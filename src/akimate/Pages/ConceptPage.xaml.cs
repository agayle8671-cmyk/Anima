using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using akimate.Services;

namespace akimate.Pages;

public sealed partial class ConceptPage : Page
{
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

    private void BtnGenerateScript_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Phase 3 — Hook up Director Agent via Semantic Kernel
        // For now, show the script panel with a placeholder
        ScriptPanel.Visibility = Visibility.Visible;
        ScriptOutput.Text = $"[Director Agent will generate script here]\n\n" +
                           $"Concept: {ConceptInput.Text}\n" +
                           $"Genre: {ProjectService.Current?.Genre}\n" +
                           $"Tone: {ProjectService.Current?.Tone}\n" +
                           $"Runtime: {ProjectService.Current?.TargetRuntimeMinutes} minutes\n" +
                           $"Episodes: {ProjectService.Current?.EpisodeCount}";
        BtnLockScript.IsEnabled = true;
    }

    private void ScriptOutput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ProjectService.Current != null)
            ProjectService.Current.ScriptText = ScriptOutput.Text;
    }

    private void BtnLockScript_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectService.Current != null)
        {
            ProjectService.Current.ConceptComplete = true;
            PhaseInfoBar.IsOpen = true;
            BtnLockScript.IsEnabled = false;
            ConceptInput.IsReadOnly = true;
        }
    }

    private void UpdateGenerateButtonState()
    {
        BtnGenerateScript.IsEnabled = ProjectService.Current != null 
                                      && !string.IsNullOrWhiteSpace(ConceptInput.Text);
    }
}
