using System.Threading;
using System.Threading.Tasks;
using akimate.Services;

namespace akimate.Agents.Plugins;

/// <summary>
/// Storyboard Agent — generates visual descriptions and image prompts
/// for each shot in the script. Works with image generation APIs
/// to create storyboard panels.
/// </summary>
public class StoryboardAgent
{
    private readonly AkimateAIEngine _engine;

    private const string SystemPrompt = @"You are a professional anime storyboard artist.
Your job is to create detailed image generation prompts for each shot in the script.

For each shot, generate a prompt that will produce an anime-style storyboard panel.
Include specifics about:
- Character poses, expressions, and positioning
- Camera angle and framing (match the shot type)
- Background and environment details
- Lighting direction and mood
- Art style (anime/manga aesthetic)

Output valid JSON array:
[
  {
    ""shot_id"": ""scene1_shot1"",
    ""prompt"": ""detailed image generation prompt"",
    ""negative_prompt"": ""things to avoid"",
    ""aspect_ratio"": ""16:9"",
    ""style_tags"": [""anime"", ""dramatic lighting""]
  }
]

Use anime-specific art terminology:
- sakuga (fluid animation), cel-shaded, line art
- Reference specific anime styles when relevant
- Include composition terms (rule of thirds, leading lines, etc.)";

    public StoryboardAgent(AkimateAIEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Generate storyboard panel prompts from a script or scene description.
    /// </summary>
    public async Task<string> GeneratePanelPromptsAsync(string sceneDescription, CancellationToken ct = default)
    {
        var userPrompt = $@"Generate detailed anime storyboard panel image prompts for the following scene:

{sceneDescription}

Create one prompt per shot. Make each prompt detailed enough for an AI image generator to produce a quality panel.";

        return await _engine.ChatAsync(SystemPrompt, userPrompt, ct);
    }

    /// <summary>
    /// Generate a character turnaround sheet prompt (front/side/back/3-quarter views).
    /// </summary>
    public async Task<string> GenerateCharacterSheetPromptAsync(string characterDescription, CancellationToken ct = default)
    {
        var userPrompt = $@"Generate an image prompt for a character turnaround reference sheet (front view, 3/4 view, side view, back view) for this character:

{characterDescription}

The prompt should produce a consistent character design across all 4 views, anime style.
Output as JSON with a single ""prompt"" and ""negative_prompt"" field.";

        return await _engine.ChatAsync(SystemPrompt, userPrompt, ct);
    }
}
