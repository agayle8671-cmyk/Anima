using System.Threading;
using System.Threading.Tasks;
using akimate.Services;

namespace akimate.Agents.Plugins;

/// <summary>
/// Layout Agent — generates Blender Python scripts to create 3D scene layouts
/// from storyboard panels. Sets up camera positions, primitive blocking geometry,
/// and environmental elements.
/// </summary>
public class LayoutAgent
{
    private readonly AkimateAIEngine _engine;

    private const string SystemPrompt = @"You are a 3D layout artist for anime production using Blender.
Your job is to generate Blender Python scripts that create 3D scene layouts.

CRITICAL: This runs in Blender --background (headless) mode. 
You MUST use bpy.data and bmesh APIs ONLY. DO NOT use bpy.ops for creating objects.
bpy.ops.mesh.primitive_* and bpy.ops.object.* SILENTLY FAIL in headless mode.

CORRECT way to create objects:
```
import bpy, bmesh
from mathutils import Vector, Euler
import math

# Create mesh object
mesh = bpy.data.meshes.new('MyBox_mesh')
obj = bpy.data.objects.new('MyBox', mesh)
bpy.context.scene.collection.objects.link(obj)
obj.location = Vector((x, y, z))
bm = bmesh.new()
bmesh.ops.create_cube(bm, size=1.0)
bm.to_mesh(mesh)
bm.free()

# Create material with Principled BSDF (for Cycles)
mat = bpy.data.materials.new('MyMat')
mat.use_nodes = True
nodes = mat.node_tree.nodes
nodes.clear()
bsdf = nodes.new('ShaderNodeBsdfPrincipled')
bsdf.inputs['Base Color'].default_value = (r, g, b, 1.0)
out = nodes.new('ShaderNodeOutputMaterial')
mat.node_tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
mesh.materials.append(mat)

# Create light
light_data = bpy.data.lights.new('MyLight', type='SUN')
light_data.energy = 3.0
light_obj = bpy.data.objects.new('MyLight', light_data)
bpy.context.scene.collection.objects.link(light_obj)

# Create camera
cam_data = bpy.data.cameras.new('MainCamera')
cam_data.lens = 50
cam_obj = bpy.data.objects.new('MainCamera', cam_data)
bpy.context.scene.collection.objects.link(cam_obj)
cam_obj.location = Vector((0, -8, 3))
cam_obj.rotation_euler = Euler((math.radians(75), 0, 0))
bpy.context.scene.camera = cam_obj
```

ALSO available bmesh primitives: create_cone, create_uvsphere, create_circle, create_grid

Always start by removing all default objects:
for obj in list(bpy.data.objects): bpy.data.objects.remove(obj, do_unlink=True)

ONLY use bpy.ops.render.render() for rendering — that one DOES work in headless mode.
Set render engine to CYCLES with device='CPU' (EEVEE fails headless).

Output ONLY valid Python code. No explanation, just code.";

    public LayoutAgent(AkimateAIEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Generate a Blender Python script to create a 3D scene layout from a shot description.
    /// </summary>
    public async Task<string> GenerateLayoutScriptAsync(string shotDescription, string cameraAngle, string locationType, CancellationToken ct = default)
    {
        var userPrompt = $@"Generate a Blender Python script to create a 3D scene layout for this shot:

SHOT: {shotDescription}
CAMERA: {cameraAngle}
LOCATION: {locationType}

Create the scene with:
1. Camera at the correct position/angle for the shot type
2. Simple primitive geometry blocking out the environment
3. Placeholder empties where characters will stand
4. A basic 3-point lighting setup matching the mood
5. Set render resolution to 1920x1080, 24fps

Output ONLY the Python code, no markdown or explanation.";

        var response = await _engine.ChatAsync(SystemPrompt, userPrompt, ct);

        // Strip any markdown code fences if the LLM adds them
        response = response.Replace("```python", "").Replace("```", "").Trim();
        return response;
    }

    /// <summary>
    /// Generate a camera placement script for a specific shot type.
    /// </summary>
    public async Task<string> GenerateCameraScriptAsync(string shotType, string cameraAngle, double[] subjectLocation, CancellationToken ct = default)
    {
        var userPrompt = $@"Generate a Blender Python script that ONLY positions the camera for:
Shot type: {shotType}
Camera angle: {cameraAngle}
Subject at: [{subjectLocation[0]}, {subjectLocation[1]}, {subjectLocation[2]}]

Point the camera at the subject. Set appropriate focal length for the shot type.
Output ONLY Python code.";

        var response = await _engine.ChatAsync(SystemPrompt, userPrompt, ct);
        response = response.Replace("```python", "").Replace("```", "").Trim();
        return response;
    }
}
