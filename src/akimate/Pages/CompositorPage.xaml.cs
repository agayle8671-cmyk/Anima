using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using akimate.Services;
using akimate.Agents.Plugins;
using System;

namespace akimate.Pages;

public sealed partial class CompositorPage : Page
{
    private SatsueiAgent? _satsuei;

    public CompositorPage()
    {
        this.InitializeComponent();
        CheckPhaseGate();
    }

    private void CheckPhaseGate()
    {
        var project = ProjectService.Current;
        if (project == null || !project.AnimaticComplete)
        {
            GateOverlay.Visibility = Visibility.Visible;
            ContentPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            GateOverlay.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Visible;
        }
    }

    private async void BtnExportVideo_Click(object sender, RoutedEventArgs e)
    {
        var project = ProjectService.Current;
        if (project == null) return;

        BtnExportVideo.IsEnabled = false;
        ExportStatus.Text = "🎬 Generating render configuration...";

        try
        {
            _satsuei ??= new SatsueiAgent(App.AIEngine);

            // Generate render config script
            var resolution = project.ExportResolution;
            var renderConfig = await _satsuei.GenerateRenderConfigScriptAsync(resolution, project.ExportFormat);

            // Generate compositing script if AI is ready
            string compositingScript = "";
            if (App.AIEngine.IsReady)
            {
                ExportStatus.Text = "🎨 Generating compositing node setup...";
                compositingScript = await _satsuei.GenerateCompositingScriptAsync(
                    sceneMood: "dramatic",
                    enableDOF: ToggleDOF?.IsOn == true,
                    enableFog: ToggleFog?.IsOn == true,
                    enableBloom: true,
                    enableColorGrading: ToggleColorGrading?.IsOn == true,
                    enableVignette: true);
            }

            // Create output directory
            var outputDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".akimate", "exports");
            System.IO.Directory.CreateDirectory(outputDir);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            if (App.Blender.IsConnected)
            {
                // Live Blender mode — execute scripts directly
                ExportStatus.Text = "✅ Configuring render in Blender...";
                await App.Blender.ExecutePythonAsync(renderConfig);

                if (!string.IsNullOrEmpty(compositingScript))
                {
                    await App.Blender.ExecutePythonAsync(compositingScript);
                    ExportStatus.Text = "✅ Compositing applied.";
                }

                var outputPath = System.IO.Path.Combine(outputDir, $"{project.Name}_{timestamp}");
                var exportScript = _satsuei.GenerateExportScript(outputPath);
                ExportStatus.Text = "🎬 Rendering... This may take a while.";
                await App.Blender.ExecutePythonAsync(exportScript);

                ExportStatus.Text = $"✅ Export complete: {outputPath}";
            }
            else
            {
                // Offline mode — save scripts to files for manual execution
                var renderScriptPath = System.IO.Path.Combine(outputDir, $"{project.Name}_{timestamp}_render_config.py");
                await System.IO.File.WriteAllTextAsync(renderScriptPath, renderConfig);

                if (!string.IsNullOrEmpty(compositingScript))
                {
                    var compScriptPath = System.IO.Path.Combine(outputDir, $"{project.Name}_{timestamp}_compositing.py");
                    await System.IO.File.WriteAllTextAsync(compScriptPath, compositingScript);
                }

                var exportScript = _satsuei.GenerateExportScript(
                    System.IO.Path.Combine(outputDir, $"{project.Name}_{timestamp}_output"));
                var exportScriptPath = System.IO.Path.Combine(outputDir, $"{project.Name}_{timestamp}_export.py");
                await System.IO.File.WriteAllTextAsync(exportScriptPath, exportScript);

                ExportStatus.Text = $"✅ Blender scripts exported to:\n{outputDir}\n\n" +
                                   "Run these scripts in Blender to render your anime:\n" +
                                   $"  1. {System.IO.Path.GetFileName(renderScriptPath)}\n" +
                                   $"  2. Compositing script (if generated)\n" +
                                   $"  3. {System.IO.Path.GetFileName(exportScriptPath)}";
            }

            // Mark phase complete
            project.CompositorComplete = true;

            // Auto-save project
            if (!string.IsNullOrEmpty(project.FilePath))
            {
                var json = ProjectService.SaveToJson(project);
                await System.IO.File.WriteAllTextAsync(project.FilePath, json);
            }
        }
        catch (Exception ex)
        {
            ExportStatus.Text = $"❌ Export failed: {ex.Message}";
        }
        finally
        {
            BtnExportVideo.IsEnabled = true;
        }
    }

    private async void BtnExportBlend_Click(object sender, RoutedEventArgs e)
    {
        var project = ProjectService.Current;
        if (project == null) return;

        BtnExportBlend.IsEnabled = false;

        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            if (App.Blender.IsConnected)
            {
                // Live mode — save .blend directly from Blender
                picker.FileTypeChoices.Add("Blender File", new[] { ".blend" });
            }
            else
            {
                // Offline mode — save the project as .aanime
                picker.FileTypeChoices.Add("akimate Project", new[] { ".aanime" });
            }

            picker.SuggestedFileName = project.Name;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                if (App.Blender.IsConnected)
                {
                    await App.Blender.SendCommandAsync("save_blend", new { filepath = file.Path });
                    ExportStatus.Text = $"✅ Blender file saved: {file.Path}";
                }
                else
                {
                    // Save the project file
                    project.FilePath = file.Path;
                    var json = ProjectService.SaveToJson(project);
                    await System.IO.File.WriteAllTextAsync(file.Path, json);
                    ExportStatus.Text = $"✅ Project saved: {file.Path}";
                }

                // Mark phase complete
                project.CompositorComplete = true;
            }
        }
        catch (Exception ex)
        {
            ExportStatus.Text = $"❌ Save failed: {ex.Message}";
        }
        finally
        {
            BtnExportBlend.IsEnabled = true;
        }
    }
}
