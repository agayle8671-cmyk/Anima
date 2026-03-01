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

    private string? FindBlenderPath()
    {
        var project = ProjectService.Current;
        if (project != null && !string.IsNullOrEmpty(project.BlenderPath) && File.Exists(project.BlenderPath))
            return project.BlenderPath;

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

    private async System.Threading.Tasks.Task<bool> EnsureBlenderConnectedAsync()
    {
        if (App.Blender.IsConnected) return true;

        var blenderPath = FindBlenderPath();
        if (blenderPath == null)
        {
            ExportStatus.Text = "❌ Blender not found.\n\nPlease install Blender or set the path in Settings.";
            return false;
        }

        ExportStatus.Text = $"🔄 Starting Blender daemon...\n({blenderPath})";

        try
        {
            await App.Blender.StartAsync(blenderPath);
            return true;
        }
        catch (Exception ex)
        {
            ExportStatus.Text = $"❌ Failed to start Blender:\n{ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Returns the demo layout + animation Python script that builds a full scene.
    /// This is self-contained — clears the default scene and creates everything from scratch.
    /// </summary>
    private string GetFullSceneBuildScript()
    {
        return @"import bpy
import math

# ========================================
# STEP 1: Clear the entire default scene
# ========================================
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

# Also clear orphan data
for block in bpy.data.meshes:
    if block.users == 0:
        bpy.data.meshes.remove(block)
for block in bpy.data.materials:
    if block.users == 0:
        bpy.data.materials.remove(block)

# ========================================
# STEP 2: Build the scene
# ========================================

# --- Ground plane ---
bpy.ops.mesh.primitive_plane_add(size=50, location=(0, 0, 0))
ground = bpy.context.active_object
ground.name = 'Ground'
mat_ground = bpy.data.materials.new('Ground_Mat')
mat_ground.diffuse_color = (0.15, 0.15, 0.18, 1)
ground.data.materials.append(mat_ground)

# --- Buildings (background) ---
buildings = [(-8, 12, 15), (-3, 14, 20), (3, 13, 12), (8, 15, 18), (13, 14, 10)]
for i, (x, y, h) in enumerate(buildings):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, h/2))
    b = bpy.context.active_object
    b.name = f'Building_{i}'
    b.scale = (2.5, 2.5, h/2)
    mat_b = bpy.data.materials.new(f'Building_Mat_{i}')
    mat_b.diffuse_color = (0.08 + i*0.02, 0.08 + i*0.01, 0.12 + i*0.02, 1)
    b.data.materials.append(mat_b)

# --- Character 1: Akira (protagonist) ---
bpy.ops.mesh.primitive_cylinder_add(radius=0.3, depth=1.7, location=(-1.5, 2, 0.85))
char1 = bpy.context.active_object
char1.name = 'Character_Akira'
mat_char1 = bpy.data.materials.new('Char1_Mat')
mat_char1.diffuse_color = (0.6, 0.2, 0.2, 1)
char1.data.materials.append(mat_char1)

# Akira's head
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.25, location=(-1.5, 2, 1.95))
head1 = bpy.context.active_object
head1.name = 'Akira_Head'
head1.data.materials.append(mat_char1)

# --- Character 2: Kuro (antagonist) ---
bpy.ops.mesh.primitive_cylinder_add(radius=0.35, depth=1.9, location=(1.5, 3, 0.95))
char2 = bpy.context.active_object
char2.name = 'Character_Kuro'
mat_char2 = bpy.data.materials.new('Char2_Mat')
mat_char2.diffuse_color = (0.1, 0.1, 0.15, 1)
char2.data.materials.append(mat_char2)

# Kuro's head
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.27, location=(1.5, 3, 2.15))
head2 = bpy.context.active_object
head2.name = 'Kuro_Head'
head2.data.materials.append(mat_char2)

# --- Key light (warm sunset) ---
bpy.ops.object.light_add(type='SUN', location=(5, -5, 10))
key_light = bpy.context.active_object
key_light.name = 'Key_Light'
key_light.data.energy = 3
key_light.data.color = (1.0, 0.85, 0.6)
key_light.rotation_euler = (math.radians(45), math.radians(15), math.radians(-30))

# --- Fill light (cool blue) ---
bpy.ops.object.light_add(type='AREA', location=(-6, -3, 5))
fill = bpy.context.active_object
fill.name = 'Fill_Light'
fill.data.energy = 50
fill.data.color = (0.5, 0.6, 1.0)
fill.data.size = 5

# --- Rim light ---
bpy.ops.object.light_add(type='SPOT', location=(0, 8, 6))
rim = bpy.context.active_object
rim.name = 'Rim_Light'
rim.data.energy = 200
rim.data.color = (0.9, 0.7, 1.0)
rim.rotation_euler = (math.radians(-60), 0, 0)

# --- Camera ---
bpy.ops.object.camera_add(location=(0, -6, 2.5))
cam = bpy.context.active_object
cam.name = 'MainCamera'
cam.rotation_euler = (math.radians(80), 0, 0)
cam.data.lens = 50
bpy.context.scene.camera = cam

# ========================================
# STEP 3: Render settings
# ========================================
scene = bpy.context.scene
scene.render.resolution_x = 1920
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.fps = 24

# Set render engine (compatible across Blender versions)
for eng in ['BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE', 'EEVEE']:
    try:
        scene.render.engine = eng
        break
    except:
        continue

scene.render.image_settings.file_format = 'PNG'

# Color management
scene.view_settings.view_transform = 'Standard'
try:
    scene.view_settings.look = 'None'
except:
    pass

# EEVEE samples
try:
    if hasattr(scene, 'eevee'):
        scene.eevee.taa_render_samples = 64
except:
    pass

print(f'SCENE BUILT: {len(bpy.data.objects)} objects, engine={scene.render.engine}')
print(f'Objects: {[o.name for o in bpy.data.objects]}')
";
    }

    private async void BtnExportVideo_Click(object sender, RoutedEventArgs e)
    {
        var project = ProjectService.Current;
        if (project == null) return;

        BtnExportVideo.IsEnabled = false;
        BtnExportBlend.IsEnabled = false;

        try
        {
            // Step 1: Auto-start Blender
            if (!await EnsureBlenderConnectedAsync())
            {
                BtnExportVideo.IsEnabled = true;
                BtnExportBlend.IsEnabled = true;
                return;
            }
            ExportStatus.Text = "✅ Blender connected!\n";

            // Step 2: Build the ENTIRE scene fresh in THIS Blender session
            // No relying on saved files from other phases
            ExportStatus.Text += "\n🏗 Step 1/3: Building 3D scene...";

            string buildScript;
            if (App.AIEngine.IsReady)
            {
                // Use AI to generate scene from the script
                var layout = new LayoutAgent(App.AIEngine);
                var sceneDesc = !string.IsNullOrEmpty(project.ScriptText) && project.ScriptText.Length > 50
                    ? project.ScriptText.Substring(0, Math.Min(500, project.ScriptText.Length))
                    : "A dramatic anime confrontation at dawn";
                buildScript = await layout.GenerateLayoutScriptAsync(
                    sceneDesc, "medium shot, eye-level", "anime cityscape");
            }
            else
            {
                buildScript = GetFullSceneBuildScript();
            }

            var buildResult = await App.Blender.ExecutePythonAsync(buildScript);
            // Check if the command returned an error
            var buildStatus = buildResult.GetProperty("status").GetString();
            if (buildStatus == "error")
            {
                var err = buildResult.GetProperty("error").GetString();
                ExportStatus.Text += $" ❌\nBlender error: {err}";
                return;
            }
            ExportStatus.Text += " ✅";

            // Step 3: Verify the scene was actually built
            var sceneInfo = await App.Blender.SendCommandAsync("scene_info");
            var objectCount = sceneInfo.GetProperty("result").GetProperty("objects").GetArrayLength();
            ExportStatus.Text += $"\n   ({objectCount} objects in scene)";

            if (objectCount <= 3) // Only default objects
            {
                ExportStatus.Text += "\n⚠ Warning: Scene may not have built correctly.";
            }

            // Step 4: Render
            ExportStatus.Text += "\n\n🎬 Step 2/3: Rendering frame...";

            var outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".akimate", "exports");
            Directory.CreateDirectory(outputDir);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputPath = Path.Combine(outputDir, $"{project.Name}_{timestamp}.png").Replace("\\", "/");

            var renderScript = $@"import bpy
scene = bpy.context.scene
scene.frame_current = 1
scene.render.filepath = r'{outputPath}'
scene.render.image_settings.file_format = 'PNG'
bpy.ops.render.render(write_still=True)
print(f'RENDER SAVED: {{scene.render.filepath}}')
";
            var renderResult = await App.Blender.ExecutePythonAsync(renderScript);
            var renderStatus = renderResult.GetProperty("status").GetString();
            if (renderStatus == "error")
            {
                var err = renderResult.GetProperty("error").GetString();
                ExportStatus.Text += $" ❌\nRender error: {err}";
                return;
            }
            ExportStatus.Text += " ✅";

            // Step 5: Verify render file exists
            ExportStatus.Text += "\n\n✅ Step 3/3: Verifying output...";
            if (File.Exists(outputPath))
            {
                var fileSize = new FileInfo(outputPath).Length;
                ExportStatus.Text += $" ✅\n\n🎉 Export complete!\nFile: {outputPath}\nSize: {fileSize:N0} bytes";
            }
            else
            {
                ExportStatus.Text += $"\n\n⚠ Render command succeeded but file not found at:\n{outputPath}";
            }

            // Mark phase complete
            project.CompositorComplete = true;
            if (!string.IsNullOrEmpty(project.FilePath))
            {
                var json = ProjectService.SaveToJson(project);
                await File.WriteAllTextAsync(project.FilePath, json);
            }
        }
        catch (Exception ex)
        {
            ExportStatus.Text = $"❌ Export failed:\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}";
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
            if (!await EnsureBlenderConnectedAsync())
            {
                BtnExportBlend.IsEnabled = true;
                BtnExportVideo.IsEnabled = true;
                return;
            }

            // Build scene first
            ExportStatus.Text = "🏗 Building scene...";
            var buildResult = await App.Blender.ExecutePythonAsync(GetFullSceneBuildScript());
            ExportStatus.Text = "✅ Scene built!";

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
                project.CompositorComplete = true;
                ExportStatus.Text = $"✅ Blender file saved: {file.Path}\n\n🎉 Production pipeline finished!";

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
