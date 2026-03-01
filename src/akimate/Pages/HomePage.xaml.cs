using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.Storage;
using System;
using akimate.Services;

namespace akimate.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        this.InitializeComponent();
    }

    private async void BtnNewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "New Project",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var panel = new StackPanel { Spacing = 12 };
        var nameBox = new TextBox
        {
            Header = "Project Name",
            PlaceholderText = "My Anime Project"
        };
        panel.Children.Add(nameBox);

        var descBox = new TextBox
        {
            Header = "Description (optional)",
            PlaceholderText = "A brief description of your project",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80
        };
        panel.Children.Add(descBox);

        dialog.Content = panel;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            var project = ProjectService.CreateNew(nameBox.Text, descBox.Text);
            ProjectService.Current = project;
            BtnSaveProject.IsEnabled = true;

            // Update title bar
            if (App.MainWindow is MainWindow mainWindow)
            {
                mainWindow.SetProjectName(project.Name);
            }

            UpdatePhaseStatuses();
        }
    }

    private async void BtnOpenProject_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".aanime");

        // Initialize the picker with the window handle
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            var json = await FileIO.ReadTextAsync(file);
            var project = ProjectService.LoadFromJson(json);
            if (project != null)
            {
                project.FilePath = file.Path;
                ProjectService.Current = project;
                BtnSaveProject.IsEnabled = true;

                if (App.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.SetProjectName(project.Name);
                }

                UpdatePhaseStatuses();
            }
        }
    }

    private async void BtnSaveProject_Click(object sender, RoutedEventArgs e)
    {
        var project = ProjectService.Current;
        if (project == null) return;

        if (string.IsNullOrEmpty(project.FilePath))
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("akimate Project", new[] { ".aanime" });
            picker.SuggestedFileName = project.Name;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                project.FilePath = file.Path;
                var json = ProjectService.SaveToJson(project);
                await FileIO.WriteTextAsync(file, json);
            }
        }
        else
        {
            var file = await StorageFile.GetFileFromPathAsync(project.FilePath);
            var json = ProjectService.SaveToJson(project);
            await FileIO.WriteTextAsync(file, json);
        }
    }

    private void UpdatePhaseStatuses()
    {
        var project = ProjectService.Current;
        if (project == null) return;

        Phase1Status.Text = project.ConceptComplete ? "Complete ✓" : "Ready";
        Phase2Status.Text = project.PreProductionComplete ? "Complete ✓" : 
                           project.ConceptComplete ? "Ready" : "Locked";
        Phase3Status.Text = project.AnimaticComplete ? "Complete ✓" : 
                           project.PreProductionComplete ? "Ready" : "Locked";
        Phase4Status.Text = project.CompositorComplete ? "Complete ✓" : 
                           project.AnimaticComplete ? "Ready" : "Locked";
    }
}
