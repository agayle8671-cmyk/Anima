using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using akimate.Services;

namespace akimate.Pages;

public sealed partial class PreProductionPage : Page
{
    public PreProductionPage()
    {
        this.InitializeComponent();
        CheckGate();
    }

    private void CheckGate()
    {
        var project = ProjectService.Current;
        bool unlocked = project != null && project.ConceptComplete;

        GateInfo.IsOpen = !unlocked;
        StoryboardSection.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
        CharacterSection.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
        BtnProceedToAnimatic.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnGenerateStoryboard_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Phase 3 — Hook up Storyboard Agent
    }

    private void BtnGenerateCharacters_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Phase 3 — Hook up Character Generation via SDXL/LoRA
    }

    private void BtnProceedToAnimatic_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectService.Current != null)
        {
            ProjectService.Current.PreProductionComplete = true;
        }
    }
}
