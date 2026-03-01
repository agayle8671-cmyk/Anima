using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using akimate.Services;

namespace akimate.Pages;

public sealed partial class CompositorPage : Page
{
    public CompositorPage()
    {
        this.InitializeComponent();
        CheckGate();
    }

    private void CheckGate()
    {
        var project = ProjectService.Current;
        bool unlocked = project != null && project.AnimaticComplete;

        GateInfo.IsOpen = !unlocked;
        RenderSection.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
        CompositingSection.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
        ExportSection.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnExportVideo_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Phase 4 — Trigger Blender render pipeline and export MP4
    }

    private void BtnExportBlend_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Phase 4 — Export the .blend project file for manual refinement
    }
}
