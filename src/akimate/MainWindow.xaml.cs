using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using akimate.Pages;

namespace akimate;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Configure window
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));

        // Set custom title bar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
    }

    /// <summary>
    /// Sets the project name in the title bar.
    /// </summary>
    public void SetProjectName(string name)
    {
        ProjectNameText.Text = string.IsNullOrEmpty(name) ? "" : $"— {name}";
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // Navigate to Home on startup
        NavView.SelectedItem = NavHome;
        ContentFrame.Navigate(typeof(HomePage), null, new EntranceNavigationTransitionInfo());
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage), null, new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
            return;
        }

        if (args.SelectedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            var pageType = tag switch
            {
                "Home" => typeof(HomePage),
                "Concept" => typeof(ConceptPage),
                "PreProduction" => typeof(PreProductionPage),
                "Animatic" => typeof(AnimaticPage),
                "Compositor" => typeof(CompositorPage),
                "Hardware" => typeof(HardwarePage),
                _ => typeof(HomePage)
            };

            // Only navigate if we're going to a different page
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType, null, new SlideNavigationTransitionInfo
                {
                    Effect = SlideNavigationTransitionEffect.FromRight
                });
            }
        }
    }
}
