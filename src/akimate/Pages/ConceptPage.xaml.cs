using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using akimate.Services;
using akimate.Agents.Plugins;
using System;

namespace akimate.Pages;

public sealed partial class ConceptPage : Page
{
    private DirectorAgent? _director;

    public ConceptPage()
    {
        this.InitializeComponent();
        LoadProjectState();
    }

    private void LoadProjectState()
    {
        var project = ProjectService.Current;
        if (project == null) return;

        ConceptInput.Text = project.ConceptRawInput;
        ScriptOutput.Text = project.ScriptText;
        RuntimeBox.Value = project.TargetRuntimeMinutes;
        EpisodeCountBox.Value = project.EpisodeCount;

        if (!string.IsNullOrEmpty(project.ScriptText))
        {
            ScriptPanel.Visibility = Visibility.Visible;
            BtnLockScript.IsEnabled = true;
        }

        UpdateGenerateButtonState();
    }

    private void ConceptInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ProjectService.Current != null)
            ProjectService.Current.ConceptRawInput = ConceptInput.Text;
        UpdateGenerateButtonState();
    }

    private void GenreSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectService.Current != null && GenreSelector.SelectedItem is ComboBoxItem item)
            ProjectService.Current.Genre = item.Content?.ToString() ?? "";
        UpdateGenerateButtonState();
    }

    private void ToneSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectService.Current != null && ToneSelector.SelectedItem is ComboBoxItem item)
            ProjectService.Current.Tone = item.Content?.ToString() ?? "";
    }

    private void RuntimeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (ProjectService.Current != null && !double.IsNaN(args.NewValue))
            ProjectService.Current.TargetRuntimeMinutes = (int)args.NewValue;
    }

    private void EpisodeCountBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (ProjectService.Current != null && !double.IsNaN(args.NewValue))
            ProjectService.Current.EpisodeCount = (int)args.NewValue;
    }

    private async void BtnGenerateScript_Click(object sender, RoutedEventArgs e)
    {
        var project = ProjectService.Current;
        if (project == null || string.IsNullOrWhiteSpace(ConceptInput.Text)) return;

        // Show progress
        BtnGenerateScript.IsEnabled = false;
        BtnGenerateScript.Content = "⏳ Generating...";
        ScriptPanel.Visibility = Visibility.Visible;

        // Check if AI engine is ready
        if (!App.AIEngine.IsReady)
        {
            // Generate a demo script so the user can see the flow works
            ScriptOutput.Text = GenerateDemoScript(
                ConceptInput.Text,
                project.Genre,
                project.Tone,
                project.TargetRuntimeMinutes);

            project.ScriptText = ScriptOutput.Text;
            BtnLockScript.IsEnabled = true;
            BtnGenerateScript.IsEnabled = true;
            BtnGenerateScript.Content = "🎬 Generate Script";
            return;
        }

        ScriptOutput.Text = "Director Agent is writing your script...\n\nThis may take 30-60 seconds.";

        try
        {
            _director ??= new DirectorAgent(App.AIEngine);

            var script = await _director.GenerateScriptAsync(
                concept: ConceptInput.Text,
                genre: project.Genre,
                tone: project.Tone,
                targetRuntimeMinutes: project.TargetRuntimeMinutes,
                episodeCount: project.EpisodeCount);

            ScriptOutput.Text = script;
            project.ScriptText = script;
            BtnLockScript.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ScriptOutput.Text = $"❌ Script generation failed:\n\n{ex.Message}\n\n" +
                               "Check your API key in Settings and try again.";
        }
        finally
        {
            BtnGenerateScript.IsEnabled = true;
            BtnGenerateScript.Content = "🎬 Generate Script";
        }
    }

    /// <summary>
    /// Generates a demo script when no AI engine is configured.
    /// This lets the user see the pipeline flow working end-to-end.
    /// </summary>
    private string GenerateDemoScript(string concept, string genre, string tone, int runtime)
    {
        var genreText = string.IsNullOrEmpty(genre) ? "Action" : genre;
        var toneText = string.IsNullOrEmpty(tone) ? "Dramatic" : tone;

        return $@"{{
  ""title"": ""Episode 1: The Beginning"",
  ""synopsis"": ""{EscapeJson(concept)}"",
  ""generated_by"": ""Demo Mode (no API key configured)"",
  ""note"": ""To use AI-generated scripts, add your OpenAI API key in Settings."",
  ""characters"": [
    {{
      ""name"": ""Akira"",
      ""role"": ""protagonist"",
      ""description"": ""A determined young warrior with spiky dark hair, a scar over the left eye, wearing a tattered battle cloak. Lean and athletic build."",
      ""personality"": ""Stoic but compassionate. Driven by a promise made to a fallen mentor.""
    }},
    {{
      ""name"": ""Yuki"",
      ""role"": ""supporting"",
      ""description"": ""A spirited girl with silver hair and piercing blue eyes. Wears a futuristic school uniform with tech gauntlets."",
      ""personality"": ""Energetic and clever. Uses humor to mask deep intelligence.""
    }},
    {{
      ""name"": ""Kuro"",
      ""role"": ""antagonist"",
      ""description"": ""Tall, imposing figure shrouded in black armor. Red glowing eyes visible through a cracked visor."",
      ""personality"": ""Calculating and ruthless. Believes destruction is necessary for rebirth.""
    }}
  ],
  ""scenes"": [
    {{
      ""scene_number"": 1,
      ""location"": ""Ruined cityscape at dawn — crumbling skyscrapers, overgrown with vines"",
      ""time_of_day"": ""dawn"",
      ""mood"": ""{toneText} — a sense of quiet before the storm"",
      ""shots"": [
        {{
          ""shot_number"": 1,
          ""type"": ""wide"",
          ""camera_angle"": ""bird's-eye"",
          ""duration_seconds"": 4,
          ""action"": ""Sweeping aerial shot of the ruined city bathed in golden dawn light. Wind carries dust and petals."",
          ""dialogue"": [],
          ""sfx"": ""Wind howling, distant metal creaking"",
          ""camera_movement"": ""tracking""
        }},
        {{
          ""shot_number"": 2,
          ""type"": ""medium"",
          ""camera_angle"": ""low-angle"",
          ""duration_seconds"": 3,
          ""action"": ""Akira stands on a rooftop edge, cloak billowing. Looks toward the horizon."",
          ""dialogue"": [
            {{
              ""character"": ""Akira"",
              ""line"": ""Today... it all changes."",
              ""emotion"": ""quiet determination""
            }}
          ],
          ""sfx"": ""Cloak flapping in wind"",
          ""camera_movement"": ""tilt-up""
        }},
        {{
          ""shot_number"": 3,
          ""type"": ""close-up"",
          ""camera_angle"": ""eye-level"",
          ""duration_seconds"": 2,
          ""action"": ""Close on Akira's eyes — they narrow with resolve. A single tear catches the light."",
          ""dialogue"": [],
          ""sfx"": ""Heartbeat"",
          ""camera_movement"": ""static""
        }}
      ]
    }},
    {{
      ""scene_number"": 2,
      ""location"": ""Underground bunker — dim fluorescent lighting, monitors lining the walls"",
      ""time_of_day"": ""morning"",
      ""mood"": ""Tense, conspiratorial"",
      ""shots"": [
        {{
          ""shot_number"": 1,
          ""type"": ""wide"",
          ""camera_angle"": ""high-angle"",
          ""duration_seconds"": 3,
          ""action"": ""Yuki sits at a bank of monitors, typing rapidly. Holographic displays flicker around her."",
          ""dialogue"": [
            {{
              ""character"": ""Yuki"",
              ""line"": ""The readings are off the charts. Whatever they're building... it's almost ready."",
              ""emotion"": ""worried but focused""
            }}
          ],
          ""sfx"": ""Keyboard clacking, electronic hums, warning beep"",
          ""camera_movement"": ""pan-right""
        }},
        {{
          ""shot_number"": 2,
          ""type"": ""over-shoulder"",
          ""camera_angle"": ""eye-level"",
          ""duration_seconds"": 3,
          ""action"": ""Over Yuki's shoulder — one monitor shows a massive structure being constructed. Red warning text flashes."",
          ""dialogue"": [
            {{
              ""character"": ""Yuki"",
              ""line"": ""Akira... you need to see this."",
              ""emotion"": ""urgent""
            }}
          ],
          ""sfx"": ""Alert siren, static crackle"",
          ""camera_movement"": ""zoom-in""
        }}
      ]
    }},
    {{
      ""scene_number"": 3,
      ""location"": ""City street — abandoned vehicles, smoke rising, Kuro's forces visible in the distance"",
      ""time_of_day"": ""afternoon"",
      ""mood"": ""Confrontational, building tension"",
      ""shots"": [
        {{
          ""shot_number"": 1,
          ""type"": ""wide"",
          ""camera_angle"": ""dutch-angle"",
          ""duration_seconds"": 3,
          ""action"": ""Akira and Yuki move through the street. Shadows of soldiers stretch across the pavement."",
          ""dialogue"": [],
          ""sfx"": ""Boots on gravel, distant rumbling"",
          ""camera_movement"": ""tracking""
        }},
        {{
          ""shot_number"": 2,
          ""type"": ""extreme-close-up"",
          ""camera_angle"": ""low-angle"",
          ""duration_seconds"": 2,
          ""action"": ""Kuro's armored boot steps into frame. Camera tilts up to reveal his imposing silhouette against a burning sky."",
          ""dialogue"": [
            {{
              ""character"": ""Kuro"",
              ""line"": ""You're too late."",
              ""emotion"": ""cold, absolute""
            }}
          ],
          ""sfx"": ""Heavy footstep impact, ominous orchestral sting"",
          ""camera_movement"": ""tilt-up""
        }},
        {{
          ""shot_number"": 3,
          ""type"": ""medium"",
          ""camera_angle"": ""eye-level"",
          ""duration_seconds"": 3,
          ""action"": ""Akira draws his blade. The camera pulls back to show both fighters facing off — the city burning behind them."",
          ""dialogue"": [
            {{
              ""character"": ""Akira"",
              ""line"": ""It's never too late."",
              ""emotion"": ""fierce resolve""
            }}
          ],
          ""sfx"": ""Blade unsheathing, wind gust, music crescendo"",
          ""camera_movement"": ""zoom-out""
        }}
      ]
    }}
  ],
  ""metadata"": {{
    ""genre"": ""{EscapeJson(genreText)}"",
    ""tone"": ""{EscapeJson(toneText)}"",
    ""target_runtime_minutes"": {runtime},
    ""total_scenes"": 3,
    ""total_shots"": 8
  }}
}}";
    }

    private static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

    private void ScriptOutput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ProjectService.Current != null)
            ProjectService.Current.ScriptText = ScriptOutput.Text;
    }

    private async void BtnLockScript_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectService.Current != null)
        {
            ProjectService.Current.ConceptComplete = true;
            PhaseInfoBar.IsOpen = true;
            BtnLockScript.IsEnabled = false;
            ConceptInput.IsReadOnly = true;

            // Auto-save project
            if (!string.IsNullOrEmpty(ProjectService.Current.FilePath))
            {
                var json = ProjectService.SaveToJson(ProjectService.Current);
                await System.IO.File.WriteAllTextAsync(ProjectService.Current.FilePath, json);
            }
        }
    }

    private void UpdateGenerateButtonState()
    {
        BtnGenerateScript.IsEnabled = ProjectService.Current != null
                                      && !string.IsNullOrWhiteSpace(ConceptInput.Text);
    }
}
