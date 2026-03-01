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

        // Check if AI engine is ready
        if (!App.AIEngine.IsReady)
        {
            ScriptPanel.Visibility = Visibility.Visible;
            ScriptOutput.Text = "⚠ AI Engine not initialized.\n\n" +
                               "Go to Settings and configure your API key or local inference endpoint first.\n\n" +
                               "Once configured, return here and click Generate Script again.";
            BtnLockScript.IsEnabled = false;
            return;
        }

        // Show progress
        BtnGenerateScript.IsEnabled = false;
        BtnGenerateScript.Content = "⏳ Generating...";
        ScriptPanel.Visibility = Visibility.Visible;
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
