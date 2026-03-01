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
Your job is to generate Blender Python (bpy) scripts that create 3D scene layouts
matching the storyboard panels. The scripts will be executed in Blender's headless mode.

When generating layout scripts, include:
1. Camera placement matching the shot type and angle from the storyboard
2. Simple primitive geometry for environmental blocking (cubes for buildings, planes for floors, etc.)
3. Placeholder objects for character positions (empty axes or cubes at proper scale)
4. Basic 3-point lighting setup appropriate for the scene mood
5. Scene frame range and FPS settings

Output ONLY valid Python code that uses Blender's bpy module. No explanation, just code.
The code should be self-contained and executable.

Camera angle reference:
- wide shot: camera far back, captures full environment
- medium shot: waist-up framing
- close-up: head and shoulders
- extreme close-up: face only
- low-angle: camera below subject, looking up (power/dominance)
- high-angle: camera above, looking down (vulnerability)
- dutch angle: camera tilted (tension/unease)
- bird's-eye: directly overhead

Always start by clearing the default scene:
import bpy
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()";

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
