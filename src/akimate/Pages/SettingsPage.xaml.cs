using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Security.Credentials;
using System;
using System.IO;

namespace akimate.Pages;

public sealed partial class SettingsPage : Page
{
    private const string VaultResource = "akimate";

    public SettingsPage()
    {
        this.InitializeComponent();
        DetectBlender();
        LoadApiKeys();
    }

    private void DetectBlender()
    {
        // Auto-detect common Blender install locations
        string[] searchPaths = new[]
        {
            @"C:\Program Files\Blender Foundation",
            @"C:\Program Files (x86)\Blender Foundation",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Blender Foundation"
        };

        foreach (var basePath in searchPaths)
        {
            if (Directory.Exists(basePath))
            {
                foreach (var dir in Directory.GetDirectories(basePath, "Blender*"))
                {
                    var exe = Path.Combine(dir, "blender.exe");
                    if (File.Exists(exe))
                    {
                        BlenderPathBox.Text = exe;
                        BlenderStatusInfo.IsOpen = true;
                        BlenderStatusInfo.Message = $"Found at: {exe}";
                        return;
                    }
                }
            }
        }

        BlenderStatusInfo.IsOpen = true;
        BlenderStatusInfo.Severity = InfoBarSeverity.Warning;
        BlenderStatusInfo.Title = "Blender Not Found";
        BlenderStatusInfo.Message = "Please browse to your Blender installation.";
    }

    private void LoadApiKeys()
    {
        try
        {
            var vault = new PasswordVault();
            var credentials = vault.FindAllByResource(VaultResource);
            foreach (var cred in credentials)
            {
                cred.RetrievePassword();
                if (cred.UserName == "runway") RunwayKeyBox.Password = cred.Password;
                if (cred.UserName == "sora") SoraKeyBox.Password = cred.Password;
            }
        }
        catch
        {
            // No credentials stored yet — that's fine
        }
    }

    private void SaveApiKey(string service, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        var vault = new PasswordVault();
        try
        {
            var existing = vault.Retrieve(VaultResource, service);
            vault.Remove(existing);
        }
        catch { /* Not found — fine */ }
        vault.Add(new PasswordCredential(VaultResource, service, key));
    }

    private void InferenceMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Inference mode preference stored in project if available
        if (Services.ProjectService.Current != null && InferenceModeSelector.SelectedItem is RadioButton rb)
        {
            Services.ProjectService.Current.InferenceMode = rb.Tag?.ToString() ?? "local";
        }
    }

    private void BtnSaveRunwayKey_Click(object sender, RoutedEventArgs e)
    {
        SaveApiKey("runway", RunwayKeyBox.Password);
    }

    private void BtnSaveSoraKey_Click(object sender, RoutedEventArgs e)
    {
        SaveApiKey("sora", SoraKeyBox.Password);
    }

    private async void BtnBrowseBlender_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".exe");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            BlenderPathBox.Text = file.Path;
            BlenderStatusInfo.IsOpen = true;
            BlenderStatusInfo.Severity = InfoBarSeverity.Success;
            BlenderStatusInfo.Title = "Blender Configured";
            BlenderStatusInfo.Message = $"Path: {file.Path}";
        }
    }
}
