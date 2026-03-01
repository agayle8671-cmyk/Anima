using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using akimate.Services;
using akimate.Agents.Plugins;
using System;

namespace akimate.Pages;

public sealed partial class PreProductionPage : Page
{
    private StoryboardAgent? _storyboard;

    public PreProductionPage()
    {
        this.InitializeComponent();
        CheckPhaseGate();
    }

    private void CheckPhaseGate()
    {
        var project = ProjectService.Current;
        if (project == null || !project.ConceptComplete)
        {
            GateOverlay.Visibility = Visibility.Visible;
            ContentPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            GateOverlay.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Visible;
        }
    }

    private async void BtnGenerateStoryboard_Click(object sender, RoutedEventArgs e)
    {
        var project = ProjectService.Current;
        if (project == null || string.IsNullOrEmpty(project.ScriptText)) return;

        if (!App.AIEngine.IsReady)
        {
            StoryboardStatus.Text = "⚠ AI Engine not initialized. Configure in Settings first.";
            return;
        }

        BtnGenerateStoryboard.IsEnabled = false;
        StoryboardStatus.Text = "🎨 Storyboard Agent generating panel prompts...";

        try
        {
            _storyboard ??= new StoryboardAgent(App.AIEngine);
            var prompts = await _storyboard.GeneratePanelPromptsAsync(project.ScriptText);
            StoryboardStatus.Text = prompts;
        }
        catch (Exception ex)
        {
            StoryboardStatus.Text = $"❌ Failed: {ex.Message}";
        }
        finally
        {
            BtnGenerateStoryboard.IsEnabled = true;
        }
    }

    private async void BtnGenerateCharacterSheets_Click(object sender, RoutedEventArgs e)
    {
        var project = ProjectService.Current;
        if (project == null || string.IsNullOrEmpty(project.ScriptText)) return;

        if (!App.AIEngine.IsReady)
        {
            CharacterSheetStatus.Text = "⚠ AI Engine not initialized. Configure in Settings first.";
            return;
        }

        BtnGenerateCharacterSheets.IsEnabled = false;
        CharacterSheetStatus.Text = "🎨 Generating character sheet prompts...";

        try
        {
            _storyboard ??= new StoryboardAgent(App.AIEngine);
            var prompts = await _storyboard.GenerateCharacterSheetPromptAsync(project.ScriptText);
            CharacterSheetStatus.Text = prompts;
        }
        catch (Exception ex)
        {
            CharacterSheetStatus.Text = $"❌ Failed: {ex.Message}";
        }
        finally
        {
            BtnGenerateCharacterSheets.IsEnabled = true;
        }
    }

    private async void BtnLockPreProduction_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectService.Current != null)
        {
            ProjectService.Current.PreProductionComplete = true;
            PhaseInfoBar.IsOpen = true;
            BtnLockPreProduction.IsEnabled = false;

            // Auto-save
            if (!string.IsNullOrEmpty(ProjectService.Current.FilePath))
            {
                var json = ProjectService.SaveToJson(ProjectService.Current);
                await System.IO.File.WriteAllTextAsync(ProjectService.Current.FilePath, json);
            }
        }
    }
}
