using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using akimate.Services;

namespace akimate.Pages;

public sealed partial class AnimaticPage : Page
{
    public AnimaticPage()
    {
        this.InitializeComponent();
        CheckGate();
    }

    private void CheckGate()
    {
        var project = ProjectService.Current;
        bool unlocked = project != null && project.PreProductionComplete;

        GateInfo.IsOpen = !unlocked;
        VoiceSection.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
        AnimationSection.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
        BtnProceedToCompositor.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnGenerateVoice_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Phase 3 — Hook up Kokoro TTS via ONNX Runtime
    }

    private void BtnProceedToCompositor_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectService.Current != null)
        {
            ProjectService.Current.AnimaticComplete = true;
        }
    }
}
