using Microsoft.UI.Xaml;
using akimate.Services;

namespace akimate;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    /// <summary>Global AI engine instance (Semantic Kernel).</summary>
    public static AkimateAIEngine AIEngine { get; } = new();

    /// <summary>Global Blender IPC service.</summary>
    public static BlenderService Blender { get; } = new();

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
