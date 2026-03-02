using System.Threading;
using System.Threading.Tasks;
using akimate.Services;

namespace akimate.Agents.Plugins;

/// <summary>
/// Animation Agent — generates Blender Python scripts for keyframe animation
/// with anime-specific techniques: stepped interpolation (not smooth splines),
/// impact frames, smear frames, and Yutapon cubes.
/// </summary>
public class AnimationAgent
{
    private readonly AkimateAIEngine _engine;

    private const string SystemPrompt = @"You are a professional anime key animator working in Blender.
Generate Python scripts that create keyframe animations using anime techniques.

CRITICAL: This runs in Blender --background (headless) mode.
- obj.keyframe_insert() WORKS in headless mode — use it freely
- bpy.ops.mesh.* and bpy.ops.object.* do NOT work — don't create objects
- Objects already exist in the scene — reference them by name via bpy.data.objects[name]

ANIME ANIMATION RULES:
1. Use CONSTANT interpolation on ALL keyframes (stepped anime look)
2. Animate on 2s (new pose every 2 frames = 12 drawings/sec)
3. Strong key poses with exaggerated anticipation and follow-through
4. Hold frames for dramatic emphasis

HOW TO ANIMATE:
```
import bpy
obj = bpy.data.objects['CharacterName']
scene = bpy.context.scene

# Set pose at frame
scene.frame_set(1)
obj.location = (x, y, z)
obj.rotation_euler = (rx, ry, rz)
obj.keyframe_insert(data_path='location', frame=1)
obj.keyframe_insert(data_path='rotation_euler', frame=1)

# Set CONSTANT interpolation for stepped look
if obj.animation_data and obj.animation_data.action:
    for fc in obj.animation_data.action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'CONSTANT'
```

Animate ALL objects in the scene — characters, camera, lights.
Create interesting camera movements (pans, zooms, tracking shots).
Add character movement that tells the story.
Output ONLY valid Python code. No markdown or explanation.";

    public AnimationAgent(AkimateAIEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Generate animation keyframes for a character action in a shot.
    /// </summary>
    public async Task<string> GenerateAnimationScriptAsync(
        string actionDescription,
        string objectName,
        int startFrame,
        int endFrame,
        string animationStyle = "on_twos",
        CancellationToken ct = default)
    {
        var userPrompt = $@"Generate a Blender Python animation script:

ACTION: {actionDescription}
OBJECT NAME: {objectName}
FRAME RANGE: {startFrame} to {endFrame}
STYLE: {animationStyle} (on_ones/on_twos/on_threes)

Create keyframes for location, rotation, and scale as needed.
Use CONSTANT interpolation on ALL keyframes for the stepped anime look.
Include anticipation, action, and follow-through poses.
Output ONLY Python code.";

        var response = await _engine.ChatAsync(SystemPrompt, userPrompt, ct);
        response = response.Replace("```python", "").Replace("```", "").Trim();
        return response;
    }

    /// <summary>
    /// Generate an impact frame script (single white/inverted frame at impact point).
    /// </summary>
    public async Task<string> GenerateImpactFrameScriptAsync(int impactFrame, CancellationToken ct = default)
    {
        var userPrompt = $@"Generate a Blender Python script that creates an impact frame effect at frame {impactFrame}:
1. Add a full-screen white plane facing the camera at frame {impactFrame}
2. Hide it on frame {impactFrame - 1} and frame {impactFrame + 1}
3. Only visible for exactly 1 frame
4. Use keyframe visibility animation
Output ONLY Python code.";

        var response = await _engine.ChatAsync(SystemPrompt, userPrompt, ct);
        response = response.Replace("```python", "").Replace("```", "").Trim();
        return response;
    }

    /// <summary>
    /// Generate a script that applies stepped interpolation to all keyframes on an object.
    /// </summary>
    public Task<string> GenerateSteppedCurvesScriptAsync(string objectName, CancellationToken ct = default)
    {
        // This one we can provide deterministically without LLM
        return Task.FromResult($@"import bpy

obj = bpy.data.objects.get('{objectName}')
if obj and obj.animation_data and obj.animation_data.action:
    action = obj.animation_data.action
    if hasattr(action, 'layers'):
        for layer in action.layers:
            for strip in layer.strips:
                if hasattr(strip, 'channelbags'):
                    for bag in strip.channelbags:
                        for fc in bag.fcurves:
                            for kp in fc.keyframe_points:
                                kp.interpolation = 'CONSTANT'
    elif hasattr(action, 'fcurves'):
        for fc in action.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = 'CONSTANT'
    print(f'Stepped curves applied to {{obj.name}}')
else:
    print(f'No animation data found on {objectName}')
");
    }
}
