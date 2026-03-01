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
        // NOTE: Uses bpy.data + bmesh API exclusively — NOT bpy.ops.
        // bpy.ops requires an active viewport context which doesn't exist in
        // Blender --background mode, so all ops calls silently do nothing.
        return @"import bpy
import bmesh
import math
from mathutils import Vector, Euler

scene = bpy.context.scene
col = scene.collection

# ============================================================
# STEP 1: Remove ALL existing objects (no bpy.ops needed)
# ============================================================
for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
for mesh in list(bpy.data.meshes):
    bpy.data.meshes.remove(mesh)
for cam in list(bpy.data.cameras):
    bpy.data.cameras.remove(cam)
for light in list(bpy.data.lights):
    bpy.data.lights.remove(light)
for mat in list(bpy.data.materials):
    bpy.data.materials.remove(mat)

def make_mat(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    return mat

def add_box(name, location, scale, color):
    mesh = bpy.data.meshes.new(name + '_mesh')
    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    obj.location = Vector(location)
    obj.scale = Vector(scale)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(mesh)
    bm.free()
    mat = make_mat(name + '_mat', color)
    mesh.materials.append(mat)
    return obj

def add_cylinder(name, location, radius, depth, color):
    mesh = bpy.data.meshes.new(name + '_mesh')
    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    obj.location = Vector(location)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False,
                          segments=16, radius1=radius, radius2=radius, depth=depth)
    bm.to_mesh(mesh)
    bm.free()
    mat = make_mat(name + '_mat', color)
    mesh.materials.append(mat)
    return obj

def add_sphere(name, location, radius, color):
    mesh = bpy.data.meshes.new(name + '_mesh')
    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    obj.location = Vector(location)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=radius)
    bm.to_mesh(mesh)
    bm.free()
    mat = make_mat(name + '_mat', color)
    mesh.materials.append(mat)
    return obj

def add_plane(name, location, size, color):
    mesh = bpy.data.meshes.new(name + '_mesh')
    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    obj.location = Vector(location)
    bm = bmesh.new()
    half = size / 2.0
    verts = [bm.verts.new(v) for v in [(-half,-half,0),(half,-half,0),(half,half,0),(-half,half,0)]]
    bm.faces.new(verts)
    bm.to_mesh(mesh)
    bm.free()
    mat = make_mat(name + '_mat', color)
    mesh.materials.append(mat)
    return obj

def add_light(name, light_type, location, energy, color):
    light_data = bpy.data.lights.new(name=name, type=light_type)
    light_data.energy = energy
    light_data.color = color
    obj = bpy.data.objects.new(name, light_data)
    col.objects.link(obj)
    obj.location = Vector(location)
    return obj, light_data

# ============================================================
# STEP 2: Build the scene
# ============================================================

# Ground
add_plane('Ground', (0, 0, 0), 50, (0.15, 0.15, 0.18))

# Buildings
buildings = [(-8,12,15), (-3,14,20), (3,13,12), (8,15,18), (13,14,10)]
for i, (x, y, h) in enumerate(buildings):
    add_box(f'Building_{i}', (x, y, h/2), (5, 5, h), (0.08+i*0.02, 0.08+i*0.01, 0.12+i*0.02))

# Characters
char1 = add_cylinder('Akira_Body', (-1.5, 2, 0.85), 0.3, 1.7, (0.6, 0.2, 0.2))
add_sphere('Akira_Head', (-1.5, 2, 1.95), 0.25, (0.6, 0.2, 0.2))
char2 = add_cylinder('Kuro_Body', (1.5, 3, 0.95), 0.35, 1.9, (0.1, 0.1, 0.15))
add_sphere('Kuro_Head', (1.5, 3, 2.15), 0.27, (0.1, 0.1, 0.15))

# Lights
key_obj, key_light = add_light('Key_Light', 'SUN', (5, -5, 10), 3.0, (1.0, 0.85, 0.6))
key_obj.rotation_euler = Euler((math.radians(45), math.radians(15), math.radians(-30)))

fill_obj, fill_light = add_light('Fill_Light', 'AREA', (-6, -3, 5), 200.0, (0.5, 0.6, 1.0))
if hasattr(fill_light, 'size'):
    fill_light.size = 5.0

rim_obj, rim_light = add_light('Rim_Light', 'SPOT', (0, 8, 6), 500.0, (0.9, 0.7, 1.0))
rim_obj.rotation_euler = Euler((math.radians(-60), 0, 0))

# Camera
cam_data = bpy.data.cameras.new('MainCamera')
cam_data.lens = 50
cam_obj = bpy.data.objects.new('MainCamera', cam_data)
col.objects.link(cam_obj)
cam_obj.location = Vector((0.0, -6.0, 2.5))
cam_obj.rotation_euler = Euler((math.radians(80), 0.0, 0.0))
scene.camera = cam_obj

# ============================================================
# STEP 3: Render settings
# ============================================================
scene.render.resolution_x = 1920
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.fps = 24
scene.render.image_settings.file_format = 'PNG'

for eng in ['BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE', 'EEVEE', 'CYCLES']:
    try:
        scene.render.engine = eng
        break
    except:
        continue

scene.view_settings.view_transform = 'Standard'

try:
    if hasattr(scene, 'eevee'):
        scene.eevee.taa_render_samples = 32
except:
    pass

names = [o.name for o in bpy.data.objects]
print(f'SCENE BUILT OK: {len(names)} objects -> {names}')
print(f'Camera: {scene.camera.name if scene.camera else None}')
print(f'Engine: {scene.render.engine}')
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
