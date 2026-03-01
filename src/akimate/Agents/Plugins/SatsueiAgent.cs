using System.Threading;
using System.Threading.Tasks;
using akimate.Services;

namespace akimate.Agents.Plugins;

/// <summary>
/// Satsuei (撮影) Agent — handles compositing and post-processing.
/// Generates Blender Python scripts for multi-pass rendering, compositing nodes,
/// DOF, volumetric effects, color grading, and final output.
/// Named after the Japanese anime production stage "satsuei" (photography/compositing).
/// </summary>
public class SatsueiAgent
{
    private readonly AkimateAIEngine _engine;

    private const string SystemPrompt = @"You are a professional anime compositing artist (撮影/satsuei).
Your job is to generate Blender Python (bpy) scripts that set up compositing nodes
and post-processing effects for final anime output.

Compositing techniques you should apply:
1. Multi-pass rendering: separate Diffuse, Glossy, AO, and Emission passes
2. Depth of Field: realistic or stylized anime DOF
3. Color grading: warm/cool tones matching the scene mood
4. Atmospheric effects: volumetric fog, dust motes, light rays
5. Bloom/glow: anime-style light bleeding on bright areas
6. Vignette: subtle darkening at frame edges for cinematic feel
7. Film grain: subtle grain for organic anime look

When generating compositing scripts:
- Enable compositor with bpy.context.scene.use_nodes = True
- Access compositor node tree via bpy.context.scene.node_tree
- Create and connect nodes programmatically
- Set render passes via bpy.context.scene.view_layers[0]
- Preserve the default Render Layers -> Composite connection as fallback

Output ONLY valid Python code using bpy. No markdown or explanation.";

    public SatsueiAgent(AkimateAIEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Generate a compositing node setup for a scene.
    /// </summary>
    public async Task<string> GenerateCompositingScriptAsync(
        string sceneMood,
        bool enableDOF = true,
        bool enableFog = false,
        bool enableBloom = true,
        bool enableColorGrading = true,
        bool enableVignette = true,
        CancellationToken ct = default)
    {
        var effects = new System.Collections.Generic.List<string>();
        if (enableDOF) effects.Add("Depth of Field");
        if (enableFog) effects.Add("Volumetric Fog");
        if (enableBloom) effects.Add("Bloom/Glow");
        if (enableColorGrading) effects.Add("Color Grading");
        if (enableVignette) effects.Add("Vignette");

        var userPrompt = $@"Generate a Blender Python script that sets up a compositing node tree for anime output:

MOOD: {sceneMood}
EFFECTS TO APPLY: {string.Join(", ", effects)}

Create a compositing node setup that:
1. Enables the compositor
2. Creates all necessary nodes
3. Connects them in the correct order
4. Sets appropriate values for the mood
5. Final output goes to the Composite node

Output ONLY Python code.";

        var response = await _engine.ChatAsync(SystemPrompt, userPrompt, ct);
        response = response.Replace("```python", "").Replace("```", "").Trim();
        return response;
    }

    /// <summary>
    /// Generate a render configuration script for final output.
    /// </summary>
    public Task<string> GenerateRenderConfigScriptAsync(
        string resolution = "1080p",
        string format = "MP4",
        string engine = "BLENDER_EEVEE",
        int fps = 24,
        CancellationToken ct = default)
    {
        var (resX, resY) = resolution switch
        {
            "720p" => (1280, 720),
            "1080p" => (1920, 1080),
            "4K" or "2160p" => (3840, 2160),
            _ => (1920, 1080)
        };

        // Deterministic — no LLM needed
        var script = $@"import bpy

scene = bpy.context.scene

# Render engine — try EEVEE Next (Blender 4.x+), fall back to EEVEE
try:
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
except:
    try:
        scene.render.engine = 'BLENDER_EEVEE'
    except:
        scene.render.engine = 'EEVEE'

# Resolution
scene.render.resolution_x = {resX}
scene.render.resolution_y = {resY}
scene.render.resolution_percentage = 100

# Frame rate
scene.render.fps = {fps}

# Output format
scene.render.image_settings.file_format = 'FFMPEG'
scene.render.ffmpeg.format = 'MPEG4'
scene.render.ffmpeg.codec = 'H264'
scene.render.ffmpeg.constant_rate_factor = 'HIGH'
scene.render.ffmpeg.audio_codec = 'AAC'

# Color management (anime-friendly)
scene.view_settings.view_transform = 'Standard'
try:
    scene.view_settings.look = 'None'
except:
    pass

# Performance
try:
    if hasattr(scene, 'eevee'):
        scene.eevee.taa_render_samples = 64
except:
    pass

print(f'Render configured: {{scene.render.resolution_x}}x{{scene.render.resolution_y}} @ {{scene.render.fps}}fps, {{scene.render.engine}}')
";
        return Task.FromResult(script);
    }

    /// <summary>
    /// Generate a script to render a single frame as a preview/proof.
    /// This is fast and won't time out the TCP connection.
    /// </summary>
    public string GenerateExportScript(string outputPath, int frameStart = 1, int frameEnd = 1)
    {
        return $@"import bpy

scene = bpy.context.scene
scene.frame_start = {frameStart}
scene.frame_end = {frameEnd}
scene.frame_current = {frameStart}
scene.render.filepath = r'{outputPath}'

# Render single frame (fast proof-of-concept)
bpy.ops.render.render(write_still=True)
print(f'Render complete: {{scene.render.filepath}}')
";
    }

    /// <summary>
    /// Generate a script to render a full animation to a video file.
    /// This should NOT be run via TCP (too slow) — save to file and run manually.
    /// </summary>
    public string GenerateFullAnimationExportScript(string outputPath, int frameStart = 1, int frameEnd = 250)
    {
        return $@"import bpy

scene = bpy.context.scene
scene.frame_start = {frameStart}
scene.frame_end = {frameEnd}
scene.render.filepath = r'{outputPath}'

# Render full animation
bpy.ops.render.render(animation=True)
print(f'Animation render complete: {{scene.render.filepath}}')
";
    }
}
