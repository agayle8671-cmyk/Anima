using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using akimate.Services;
using akimate.Agents.Plugins;
using System;
using System.IO;

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

    /// <summary>
    /// Finds the Blender executable on the system.
    /// Checks project settings first, then common install paths.
    /// </summary>
    private string? FindBlenderPath()
    {
        // 1. Check project settings
        var project = ProjectService.Current;
        if (project != null && !string.IsNullOrEmpty(project.BlenderPath) && File.Exists(project.BlenderPath))
            return project.BlenderPath;

        // 2. Search common install paths
        string[] searchPaths = new[]
        {
            @"C:\Program Files\Blender Foundation",
            @"C:\Program Files (x86)\Blender Foundation",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation")
        };

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath)) continue;
            foreach (var dir in Directory.GetDirectories(basePath, "Blender*"))
            {
                var exe = Path.Combine(dir, "blender.exe");
                if (File.Exists(exe)) return exe;
            }
        }

        return null;
    }

    /// <summary>
    /// Ensures Blender is running and connected.
    /// Auto-starts the daemon if not already connected.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> EnsureBlenderConnectedAsync()
    {
        if (App.Blender.IsConnected) return true;

        var blenderPath = FindBlenderPath();
        if (blenderPath == null)
        {
            ExportStatus.Text = "❌ Blender not found.\n\nPlease set the Blender path in Settings → Blender Installation.";
            return false;
        }

        ExportStatus.Text = $"🔄 Starting Blender daemon...\n({blenderPath})";

        try
        {
            await App.Blender.StartAsync(blenderPath);
            ExportStatus.Text = "✅ Blender daemon started and connected!";
            return true;
        }
        catch (Exception ex)
        {
            ExportStatus.Text = $"❌ Failed to start Blender:\n{ex.Message}\n\n" +
                               "Make sure Blender is installed and the path is correct in Settings.";
            return false;
        }
    }

    private async void BtnExportVideo_Click(object sender, RoutedEventArgs e)
    {
        var project = ProjectService.Current;
        if (project == null) return;

        BtnExportVideo.IsEnabled = false;
        BtnExportBlend.IsEnabled = false;

        try
        {
            // Auto-start Blender if needed
            if (!await EnsureBlenderConnectedAsync())
            {
                BtnExportVideo.IsEnabled = true;
                BtnExportBlend.IsEnabled = true;
                return;
            }

            _satsuei ??= new SatsueiAgent(App.AIEngine);

            // Load the scene saved from Phase 3
            var savedScene = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".akimate", "scene.blend").Replace("\\", "/");
            if (File.Exists(savedScene))
            {
                ExportStatus.Text = "📂 Loading scene from Phase 3...";
                await App.Blender.ExecutePythonAsync(
                    $"import bpy\nbpy.ops.wm.open_mainfile(filepath=r'{savedScene}')");
                ExportStatus.Text = "✅ Scene loaded!";
            }

            // Generate and apply render config
            ExportStatus.Text = "🎬 Step 1/3: Configuring render settings...";
            var resolution = project.ExportResolution;
            var renderConfig = await _satsuei.GenerateRenderConfigScriptAsync(resolution, project.ExportFormat);
            await App.Blender.ExecutePythonAsync(renderConfig);

            // Generate and apply compositing
            ExportStatus.Text = "🎨 Step 2/3: Building compositing nodes...";
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
            }

            // Export
            var outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".akimate", "exports");
            Directory.CreateDirectory(outputDir);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputPath = Path.Combine(outputDir, $"{project.Name}_{timestamp}");

            ExportStatus.Text = "🎬 Step 3/3: Rendering animation...\nThis may take a few minutes.";
            var exportScript = _satsuei.GenerateExportScript(outputPath);
            await App.Blender.ExecutePythonAsync(exportScript);

            // Mark phase complete
            project.CompositorComplete = true;
            ExportStatus.Text = $"✅ Export complete!\n\nOutput: {outputPath}\n\n🎉 Production pipeline finished!";

            // Auto-save project
            if (!string.IsNullOrEmpty(project.FilePath))
            {
                var json = ProjectService.SaveToJson(project);
                await File.WriteAllTextAsync(project.FilePath, json);
            }
        }
        catch (Exception ex)
        {
            ExportStatus.Text = $"❌ Export failed:\n{ex.Message}";
        }
        finally
        {
            BtnExportVideo.IsEnabled = true;
            BtnExportBlend.IsEnabled = true;
        }
    }

    private async void BtnExportBlend_Click(object sender, RoutedEventArgs e)
    {
        var project = ProjectService.Current;
        if (project == null) return;

        BtnExportBlend.IsEnabled = false;
        BtnExportVideo.IsEnabled = false;

        try
        {
            // Auto-start Blender if needed
            if (!await EnsureBlenderConnectedAsync())
            {
                BtnExportBlend.IsEnabled = true;
                BtnExportVideo.IsEnabled = true;
                return;
            }

            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Blender File", new[] { ".blend" });
            picker.SuggestedFileName = project.Name;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                ExportStatus.Text = "💾 Saving .blend file...";
                await App.Blender.SendCommandAsync("save_blend", new { filepath = file.Path });

                // Mark phase complete
                project.CompositorComplete = true;
                ExportStatus.Text = $"✅ Blender file saved: {file.Path}\n\n🎉 Production pipeline finished!";

                // Auto-save project
                if (!string.IsNullOrEmpty(project.FilePath))
                {
                    var json = ProjectService.SaveToJson(project);
                    await File.WriteAllTextAsync(project.FilePath, json);
                }
            }
        }
        catch (Exception ex)
        {
            ExportStatus.Text = $"❌ Save failed:\n{ex.Message}";
        }
        finally
        {
            BtnExportBlend.IsEnabled = true;
            BtnExportVideo.IsEnabled = true;
        }
    }
}
