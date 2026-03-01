using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using akimate.Services;
using akimate.Agents.Plugins;
using System;
using System.IO;
using System.Linq;

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
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Roughness'].default_value = 0.7
    out = nodes.new('ShaderNodeOutputMaterial')
    mat.node_tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
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
# STEP 3: Render settings — use CYCLES (works headless, no GPU needed)
# ============================================================
scene.render.resolution_x = 1920
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.fps = 24
scene.render.image_settings.file_format = 'PNG'

# CYCLES works in headless/background mode. EEVEE requires a GPU display and fails silently.
scene.render.engine = 'CYCLES'
try:
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 64
    scene.cycles.preview_samples = 32
except Exception as e:
    print(f'Cycles config warning: {e}')

scene.view_settings.view_transform = 'Filmic'

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

            // Step 4: Render PNG sequence (Blender 5 removed FFMPEG from image_settings enum)
            var outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".akimate", "exports");
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var framesDir = Path.Combine(outputDir, $"frames_{timestamp}").Replace("\\", "/");
            var outputMp4 = Path.Combine(outputDir, $"{project.Name}_{timestamp}.mp4").Replace("\\", "/");
            Directory.CreateDirectory(framesDir);

            ExportStatus.Text += "\n\n🎬 Step 2/3: Rendering frames...\n   (120 frames @ 24fps — takes several minutes)";

            var renderScript =
                "import bpy, os, subprocess, sys\n" +
                "scene = bpy.context.scene\n" +
                "scene.frame_start = 1\n" +
                "scene.frame_end = 120\n" +
                "scene.render.fps = 24\n" +
                "scene.cycles.samples = 16\n" +
                "scene.render.image_settings.file_format = 'PNG'\n" +
                $"frames_dir = r'{framesDir}'\n" +
                "os.makedirs(frames_dir, exist_ok=True)\n" +
                "scene.render.filepath = frames_dir + '/frame_'\n" +
                "print(f'Rendering {scene.frame_end} frames to: ' + frames_dir)\n" +
                "bpy.ops.render.render(animation=True, write_still=False)\n" +
                "print('PNG SEQUENCE DONE')\n" +
                "\n" +
                "# Find Blender's bundled ffmpeg or system ffmpeg\n" +
                "import glob\n" +
                "blender_exe = sys.argv[0]\n" +
                "blender_dir = os.path.dirname(blender_exe)\n" +
                "ffmpeg_candidates = [\n" +
                "    os.path.join(blender_dir, 'ffmpeg.exe'),\n" +
                "    os.path.join(blender_dir, 'ffmpeg'),\n" +
                "    'ffmpeg',\n" +
                "]\n" +
                "ffmpeg = None\n" +
                "for f in ffmpeg_candidates:\n" +
                "    try:\n" +
                "        r = subprocess.run([f, '-version'], capture_output=True, timeout=5)\n" +
                "        if r.returncode == 0:\n" +
                "            ffmpeg = f\n" +
                "            break\n" +
                "    except:\n" +
                "        continue\n" +
                "\n" +
                $"output_mp4 = r'{outputMp4}'\n" +
                "if ffmpeg:\n" +
                "    frame_pattern = frames_dir + '/frame_%04d.png'\n" +
                "    cmd = [ffmpeg, '-y', '-framerate', '24', '-i', frame_pattern,\n" +
                "           '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '23', output_mp4]\n" +
                "    result = subprocess.run(cmd, capture_output=True, text=True, timeout=300)\n" +
                "    if result.returncode == 0:\n" +
                "        print(f'VIDEO ENCODED: {output_mp4}')\n" +
                "    else:\n" +
                "        print(f'FFMPEG_ERROR: {result.stderr[:500]}')\n" +
                "else:\n" +
                "    print(f'FFMPEG_NOT_FOUND: frames at {frames_dir}')\n";

            using var longCts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(30));
            var renderResult = await App.Blender.ExecutePythonAsync(renderScript, longCts.Token);
            var renderStatus = renderResult.GetProperty("status").GetString();
            if (renderStatus == "error")
            {
                var err = renderResult.GetProperty("error").GetString();
                ExportStatus.Text += $"\n\n❌ Render error:\n{err}";
                return;
            }

            // Parse output to determine outcome
            var output = renderResult.GetProperty("result").GetProperty("output").GetString() ?? "";
            ExportStatus.Text = ExportStatus.Text.Replace("(120 frames @ 24fps — takes several minutes)", "✅");
            ExportStatus.Text += "\n\n✅ Step 3/3: Verifying output...";

            if (output.Contains("VIDEO ENCODED"))
            {
                var fileSize = File.Exists(outputMp4) ? new FileInfo(outputMp4).Length : 0;
                ExportStatus.Text += $" ✅\n\n🎉 Export complete!\nVideo: {outputMp4}\nSize: {fileSize:N0} bytes";
            }
            else if (output.Contains("FFMPEG_NOT_FOUND"))
            {
                ExportStatus.Text += $" ⚠\n\nFrames rendered! FFmpeg not found for MP4 encoding.\nPNG frames are at:\n{framesDir}\n\nInstall FFmpeg or use a video editor to encode them.";
            }
            else if (output.Contains("FFMPEG_ERROR"))
            {
                ExportStatus.Text += $" ⚠\n\nFrames rendered but FFmpeg encoding failed.\nPNG frames at: {framesDir}";
            }
            else
            {
                // Check manually
                if (File.Exists(outputMp4))
                {
                    var fileSize = new FileInfo(outputMp4).Length;
                    ExportStatus.Text += $" ✅\n\n🎉 Export complete!\nVideo: {outputMp4}\nSize: {fileSize:N0} bytes";
                }
                else if (Directory.Exists(framesDir) && Directory.GetFiles(framesDir, "*.png").Length > 0)
                {
                    var frameCount = Directory.GetFiles(framesDir, "*.png").Length;
                    ExportStatus.Text += $" ✅\n\n🎬 {frameCount} frames rendered!\nFrames: {framesDir}\n\nEncode to MP4: ffmpeg -framerate 24 -i frame_%04d.png output.mp4";
                }
                else
                {
                    ExportStatus.Text += $" ⚠\nOutput: {output}";
                }
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
