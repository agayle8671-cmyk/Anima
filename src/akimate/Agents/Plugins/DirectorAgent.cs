using System.Threading;
using System.Threading.Tasks;
using akimate.Services;

namespace akimate.Agents.Plugins;

/// <summary>
/// Director Agent — the screenwriter/director of the production.
/// Takes a raw concept from the user and produces a structured anime script
/// with scene breakdowns, shot lists, dialogue, and character descriptions.
/// </summary>
public class DirectorAgent
{
    private readonly AkimateAIEngine _engine;

    private const string SystemPrompt = @"You are a professional anime director and screenwriter working on an anime production.
Your job is to take the user's creative concept and transform it into a structured production script.

When generating a script, you MUST output valid JSON with this exact structure:
{
  ""title"": ""Episode Title"",
  ""synopsis"": ""Brief synopsis"",
  ""characters"": [
    {
      ""name"": ""Character Name"",
      ""role"": ""protagonist/antagonist/supporting"",
      ""description"": ""Physical appearance description for the artist"",
      ""personality"": ""Character personality and motivations""
    }
  ],
  ""scenes"": [
    {
      ""scene_number"": 1,
      ""location"": ""Description of the location"",
      ""time_of_day"": ""dawn/morning/afternoon/evening/night"",
      ""mood"": ""Emotional atmosphere"",
      ""shots"": [
        {
          ""shot_number"": 1,
          ""type"": ""wide/medium/close-up/extreme-close-up/over-shoulder/POV"",
          ""camera_angle"": ""eye-level/low-angle/high-angle/dutch-angle/bird's-eye"",
          ""duration_seconds"": 3,
          ""action"": ""What happens in this shot"",
          ""dialogue"": [
            {
              ""character"": ""Character Name"",
              ""line"": ""What they say"",
              ""emotion"": ""How they say it""
            }
          ],
          ""sfx"": ""Sound effects for this shot"",
          ""camera_movement"": ""static/pan-left/pan-right/tilt-up/tilt-down/zoom-in/zoom-out/tracking""
        }
      ]
    }
  ]
}

Follow traditional anime production conventions:
- Vary shot types for visual interest (mix wide establishing shots with close-ups for emotion)
- Use camera angles that convey power dynamics and emotion
- Include specific visual directions that help the storyboard artist
- Write dialogue that sounds natural for anime (can include Japanese honorifics if appropriate)
- Consider pacing — action scenes need faster cuts, emotional scenes need longer holds
- Include specific SFX descriptions for the sound designer
- Target the runtime and episode count specified by the user";

    public DirectorAgent(AkimateAIEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Generate a full structured script from a raw concept.
    /// </summary>
    public async Task<string> GenerateScriptAsync(
        string concept,
        string genre = "Action",
        string tone = "Dramatic",
        int targetRuntimeMinutes = 10,
        int episodeCount = 1,
        CancellationToken ct = default)
    {
        var userPrompt = $@"Create an anime script based on this concept:

CONCEPT: {concept}
GENRE: {genre}
TONE: {tone}
TARGET RUNTIME: {targetRuntimeMinutes} minutes per episode
EPISODES: {episodeCount}

Generate Episode 1's complete script in the JSON format specified. Include at least 3-5 scenes with 2-4 shots each. Make it vivid and cinematic.";

        return await _engine.ChatAsync(SystemPrompt, userPrompt, ct);
    }

    /// <summary>
    /// Break down an existing script into individual scene descriptions for the storyboard artist.
    /// </summary>
    public async Task<string> BreakdownScenesAsync(string scriptJson, CancellationToken ct = default)
    {
        var prompt = $@"Given this script JSON, create detailed visual descriptions for each shot that a storyboard artist can use to draw panels. For each shot, describe:
1. The exact framing and composition
2. Character positions and expressions
3. Background elements
4. Lighting direction and quality
5. Any special visual effects

Script:
{scriptJson}

Output as a JSON array of shot descriptions.";

        return await _engine.ChatAsync(SystemPrompt, prompt, ct);
    }

    /// <summary>
    /// Refine or edit a portion of the script based on user feedback.
    /// </summary>
    public async Task<string> RefineScriptAsync(string currentScript, string userFeedback, CancellationToken ct = default)
    {
        var prompt = $@"The user wants to modify the current script. Apply their feedback and return the updated complete script JSON.

Current Script:
{currentScript}

User's Feedback:
{userFeedback}

Return the complete updated script in the same JSON format.";

        return await _engine.ChatAsync(SystemPrompt, prompt, ct);
    }
}
