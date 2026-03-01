using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using akimate.Services;
using akimate.Agents.Plugins;
using System;

namespace akimate.Pages;

public sealed partial class AnimaticPage : Page
{
    private AnimationAgent? _animation;

    public AnimaticPage()
    {
        this.InitializeComponent();
        CheckPhaseGate();
    }

    private void CheckPhaseGate()
    {
        var project = ProjectService.Current;
        if (project == null || !project.PreProductionComplete)
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

    private async void BtnGenerateAnimation_Click(object sender, RoutedEventArgs e)
    {
        if (!App.AIEngine.IsReady)
        {
            AnimationStatus.Text = "⚠ AI Engine not initialized. Configure in Settings first.";
            return;
        }

        BtnGenerateAnimation.IsEnabled = false;
        AnimationStatus.Text = "🎬 Animation Agent generating keyframe script...";

        try
        {
            _animation ??= new AnimationAgent(App.AIEngine);

            var fps = (int)(FrameRateSelector.SelectedItem is ComboBoxItem item
                ? int.Parse(item.Tag?.ToString() ?? "24") : 24);
            var style = InterpolationToggle?.IsOn == true ? "on_ones" : "on_twos";

            var script = await _animation.GenerateAnimationScriptAsync(
                actionDescription: "Character walks forward and stops",
                objectName: "Character",
                startFrame: 1,
                endFrame: fps * 3, // 3 seconds
                animationStyle: style);

            AnimationStatus.Text = $"✅ Animation script generated ({script.Length} chars)\n\n{script}";

            // If Blender is connected, execute the script
            if (App.Blender.IsConnected)
            {
                await App.Blender.ExecutePythonAsync(script);
                AnimationStatus.Text += "\n\n✅ Script executed in Blender.";
            }
        }
        catch (Exception ex)
        {
            AnimationStatus.Text = $"❌ Failed: {ex.Message}";
        }
        finally
        {
            BtnGenerateAnimation.IsEnabled = true;
        }
    }

    private async void BtnLockAnimation_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectService.Current != null)
        {
            ProjectService.Current.AnimaticComplete = true;
            PhaseInfoBar.IsOpen = true;
            BtnLockAnimation.IsEnabled = false;

            // Auto-save
            if (!string.IsNullOrEmpty(ProjectService.Current.FilePath))
            {
                var json = ProjectService.SaveToJson(ProjectService.Current);
                await System.IO.File.WriteAllTextAsync(ProjectService.Current.FilePath, json);
            }
        }
    }
}
