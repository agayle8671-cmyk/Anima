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
Your job is to generate Blender Python (bpy) scripts that create keyframe animations
using traditional anime techniques.

CRITICAL ANIME ANIMATION RULES:
1. Use CONSTANT (stepped) interpolation on ALL keyframes — this is the #1 rule of anime animation
2. Animate on 2s or 3s (one drawing per 2-3 frames), NOT on 1s unless it's a sakuga moment
3. Use strong key poses with minimal in-betweens
4. Exaggerate anticipation and follow-through
5. Impact frames: single white or inverted-color frames at moment of impact
6. Smear frames: stretched/distorted drawings during fast motion
7. Hold frames: extend key poses for dramatic emphasis

Frame rate: 24fps (anime standard)
- On 2s = new pose every 2 frames (12 drawings/second)
- On 3s = new pose every 3 frames (8 drawings/second)
- On 1s = every frame (sakuga/fluid animation, use sparingly)

When generating animation scripts:
- Set all keyframe interpolation to 'CONSTANT' for stepped look
- Use bpy.data.objects[name].keyframe_insert()
- Access fcurves through action.layers for Blender 5.0 compatibility
- Set frame range appropriately
- Include comments explaining the timing

Output ONLY valid Python code using bpy. No markdown or explanation.";

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
