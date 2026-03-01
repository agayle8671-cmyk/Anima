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

        if (!App.Blender.IsConnected)
        {
            ExportStatus.Text = "⚠ Blender not connected. Start Blender from Settings first.";
            return;
        }

        BtnExportVideo.IsEnabled = false;
        ExportStatus.Text = "🎬 Configuring render...";

        try
        {
            _satsuei ??= new SatsueiAgent(App.AIEngine);

            // Generate and apply render config
            var resolution = project.ExportResolution;
            var renderConfig = await _satsuei.GenerateRenderConfigScriptAsync(resolution, project.ExportFormat);
            await App.Blender.ExecutePythonAsync(renderConfig);
            ExportStatus.Text = "✅ Render configured. Compositing...";

            // Generate and apply compositing
            if (App.AIEngine.IsReady)
            {
                var compositingScript = await _satsuei.GenerateCompositingScriptAsync(
                    sceneMood: "dramatic",
                    enableDOF: ToggleDOF?.IsOn == true,
                    enableFog: ToggleFog?.IsOn == true,
                    enableBloom: true,
                    enableColorGrading: ToggleColorGrading?.IsOn == true,
                    enableVignette: true);
                await App.Blender.ExecutePythonAsync(compositingScript);
                ExportStatus.Text = "✅ Compositing applied.";
            }

            // Export
            var outputDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".akimate", "exports");
            System.IO.Directory.CreateDirectory(outputDir);
            var outputPath = System.IO.Path.Combine(outputDir, $"{project.Name}_{DateTime.Now:yyyyMMdd_HHmmss}");

            var exportScript = _satsuei.GenerateExportScript(outputPath);
            ExportStatus.Text = "🎬 Rendering... This may take a while.";
            await App.Blender.ExecutePythonAsync(exportScript);

            ExportStatus.Text = $"✅ Export complete: {outputPath}";
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

        if (!App.Blender.IsConnected)
        {
            ExportStatus.Text = "⚠ Blender not connected.";
            return;
        }

        BtnExportBlend.IsEnabled = false;
        ExportStatus.Text = "💾 Saving .blend file...";

        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Blender File", new[] { ".blend" });
            picker.SuggestedFileName = $"{project.Name}";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                await App.Blender.SendCommandAsync("save_blend", new { filepath = file.Path });
                ExportStatus.Text = $"✅ Saved: {file.Path}";

                // Mark phase complete
                project.CompositorComplete = true;

                // Auto-save project
                if (!string.IsNullOrEmpty(project.FilePath))
                {
                    var json = ProjectService.SaveToJson(project);
                    await System.IO.File.WriteAllTextAsync(project.FilePath, json);
                }
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
