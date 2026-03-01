using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using akimate.Services;
using akimate.Agents.Plugins;
using System;
using System.IO;

namespace akimate.Pages;

public sealed partial class AnimaticPage : Page
{
    private AnimationAgent? _animation;
    private LayoutAgent? _layout;

    public AnimaticPage()
    {
        this.InitializeComponent();
        CheckPhaseGate();
    }

    private void CheckPhaseGate()
    {
        var project = ProjectService.Current;
        if (project == null || !project.PreProductionComplete)
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
    /// Finds Blender on the system.
    /// </summary>
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

    /// <summary>
    /// Ensures Blender daemon is running and connected.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> EnsureBlenderConnectedAsync()
    {
        if (App.Blender.IsConnected) return true;

        var blenderPath = FindBlenderPath();
        if (blenderPath == null)
        {
            AnimationStatus.Text = "❌ Blender not found. Set the path in Settings.";
            return false;
        }

        AnimationStatus.Text = $"🔄 Starting Blender daemon...";
        try
        {
            await App.Blender.StartAsync(blenderPath);
            return true;
        }
        catch (Exception ex)
        {
            AnimationStatus.Text = $"❌ Failed to start Blender: {ex.Message}";
            return false;
        }
    }

    private async void BtnGenerateAnimation_Click(object sender, RoutedEventArgs e)
    {
        var project = ProjectService.Current;
        if (project == null) return;

        BtnGenerateAnimation.IsEnabled = false;

        try
        {
            // Step 1: Auto-launch Blender
            if (!await EnsureBlenderConnectedAsync())
            {
                BtnGenerateAnimation.IsEnabled = true;
                return;
            }

            AnimationStatus.Text = "✅ Blender connected!\n";

            // Step 2: Build the 3D scene using LayoutAgent
            AnimationStatus.Text += "\n🏗 Step 1/3: Building 3D scene layout...";

            string layoutScript;
            if (App.AIEngine.IsReady)
            {
                _layout ??= new LayoutAgent(App.AIEngine);

                // Extract first scene description from the script
                var sceneDesc = !string.IsNullOrEmpty(project.ScriptText) && project.ScriptText.Length > 100
                    ? project.ScriptText.Substring(0, Math.Min(500, project.ScriptText.Length))
                    : "A dramatic anime scene with characters facing off in a ruined cityscape at dawn";

                layoutScript = await _layout.GenerateLayoutScriptAsync(
                    shotDescription: sceneDesc,
                    cameraAngle: "medium shot, eye-level",
                    locationType: "anime cityscape");
            }
            else
            {
                // Demo layout script — builds a simple but interesting scene
                layoutScript = GenerateDemoLayoutScript();
            }

            await App.Blender.ExecutePythonAsync(layoutScript);
            AnimationStatus.Text += " ✅";

            // Step 3: Generate and execute animation
            AnimationStatus.Text += "\n🎬 Step 2/3: Animating scene...";

            string animScript;
            var fps = (int)(FrameRateSelector?.SelectedItem is ComboBoxItem item
                ? int.Parse(item.Tag?.ToString() ?? "24") : 24);
            var style = InterpolationToggle?.IsOn == true ? "on_ones" : "on_twos";

            if (App.AIEngine.IsReady)
            {
                _animation ??= new AnimationAgent(App.AIEngine);
                animScript = await _animation.GenerateAnimationScriptAsync(
                    actionDescription: "Camera slowly pans across the scene, characters shift weight subtly",
                    objectName: "Camera",
                    startFrame: 1,
                    endFrame: fps * 3,
                    animationStyle: style);
            }
            else
            {
                animScript = GenerateDemoAnimationScript(fps);
            }

            await App.Blender.ExecutePythonAsync(animScript);
            AnimationStatus.Text += " ✅";

            AnimationStatus.Text += "\n\n✅ Step 3/3: Scene built and animated in Blender!";
            AnimationStatus.Text += "\n\n➡ Lock this phase and proceed to Phase 4 to render your final output.";

            BtnLockAnimation.IsEnabled = true;
        }
        catch (Exception ex)
        {
            AnimationStatus.Text = $"❌ Failed: {ex.Message}";
        }
        finally
        {
            BtnGenerateAnimation.IsEnabled = true;
        }
    }

    /// <summary>
    /// Demo layout: builds an anime-inspired scene with ground, buildings, characters, and camera.
    /// </summary>
    private string GenerateDemoLayoutScript()
    {
        return @"import bpy
import math

# Clear the default scene
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

# --- Ground plane ---
bpy.ops.mesh.primitive_plane_add(size=50, location=(0, 0, 0))
ground = bpy.context.active_object
ground.name = 'Ground'
mat_ground = bpy.data.materials.new('Ground_Mat')
mat_ground.diffuse_color = (0.15, 0.15, 0.18, 1)
ground.data.materials.append(mat_ground)

# --- Buildings (background) ---
for i, (x, y, h) in enumerate([(-8, 12, 15), (-3, 14, 20), (3, 13, 12), (8, 15, 18), (13, 14, 10)]):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, h/2))
    b = bpy.context.active_object
    b.name = f'Building_{i}'
    b.scale = (2.5, 2.5, h/2)
    mat_b = bpy.data.materials.new(f'Building_Mat_{i}')
    mat_b.diffuse_color = (0.08 + i*0.02, 0.08 + i*0.01, 0.12 + i*0.02, 1)
    b.data.materials.append(mat_b)

# --- Character 1 (protagonist) ---
bpy.ops.mesh.primitive_cylinder_add(radius=0.3, depth=1.7, location=(-1.5, 2, 0.85))
char1 = bpy.context.active_object
char1.name = 'Character_Akira'
mat_char1 = bpy.data.materials.new('Char1_Mat')
mat_char1.diffuse_color = (0.6, 0.2, 0.2, 1)
char1.data.materials.append(mat_char1)
# Head
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.25, location=(-1.5, 2, 1.95))
head1 = bpy.context.active_object
head1.name = 'Akira_Head'
head1.data.materials.append(mat_char1)

# --- Character 2 (antagonist) ---
bpy.ops.mesh.primitive_cylinder_add(radius=0.35, depth=1.9, location=(1.5, 3, 0.95))
char2 = bpy.context.active_object
char2.name = 'Character_Kuro'
mat_char2 = bpy.data.materials.new('Char2_Mat')
mat_char2.diffuse_color = (0.1, 0.1, 0.15, 1)
char2.data.materials.append(mat_char2)
# Head
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
cam.name = 'Camera'
cam.rotation_euler = (math.radians(80), 0, 0)
cam.data.lens = 50
bpy.context.scene.camera = cam

# --- Render settings ---
bpy.context.scene.render.resolution_x = 1920
bpy.context.scene.render.resolution_y = 1080
bpy.context.scene.render.fps = 24
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 72

print('Scene layout complete: 2 characters, 5 buildings, 3-point lighting, camera')
";
    }

    /// <summary>
    /// Demo animation: camera pan + character movement.
    /// </summary>
    private string GenerateDemoAnimationScript(int fps)
    {
        var totalFrames = fps * 3; // 3 seconds
        return $@"import bpy
import math

scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = {totalFrames}

# Animate camera — slow dolly in
cam = bpy.data.objects.get('Camera')
if cam:
    scene.frame_set(1)
    cam.location = (0, -8, 2.5)
    cam.keyframe_insert(data_path='location', frame=1)
    
    scene.frame_set({totalFrames})
    cam.location = (0, -4, 2.0)
    cam.keyframe_insert(data_path='location', frame={totalFrames})
    
    # Make it smooth
    if cam.animation_data and cam.animation_data.action:
        for fc in cam.animation_data.action.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = 'BEZIER'

# Subtle character animation
akira = bpy.data.objects.get('Character_Akira')
if akira:
    scene.frame_set(1)
    akira.rotation_euler = (0, 0, 0)
    akira.keyframe_insert(data_path='rotation_euler', frame=1)
    
    scene.frame_set({totalFrames // 2})
    akira.rotation_euler = (0, 0, math.radians(-15))
    akira.keyframe_insert(data_path='rotation_euler', frame={totalFrames // 2})
    
    scene.frame_set({totalFrames})
    akira.rotation_euler = (0, 0, math.radians(5))
    akira.keyframe_insert(data_path='rotation_euler', frame={totalFrames})

print(f'Animation complete: {{scene.frame_end - scene.frame_start + 1}} frames @ {fps}fps')
";
    }

    private async void BtnLockAnimation_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectService.Current != null)
        {
            ProjectService.Current.AnimaticComplete = true;
            PhaseInfoBar.IsOpen = true;
            BtnLockAnimation.IsEnabled = false;

            // Auto-save
            if (!string.IsNullOrEmpty(ProjectService.Current.FilePath))
            {
                var json = ProjectService.SaveToJson(ProjectService.Current);
                await System.IO.File.WriteAllTextAsync(ProjectService.Current.FilePath, json);
            }
        }
    }
}
