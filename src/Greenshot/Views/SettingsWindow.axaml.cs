using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;

namespace Greenshot.Views;

public partial class SettingsWindow : Window
{
    private readonly ICoreConfiguration _config;

    public SettingsWindow(ICoreConfiguration config)
    {
        _config = config;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        ChkMousePointer.IsChecked = _config.CaptureMousePointer;
        NudDelay.Value = _config.CaptureDelay;
        ChkShowNotification.IsChecked = _config.ShowTrayNotification;
        ChkPlaySound.IsChecked = _config.PlayCameraSound;

        TxtOutputPath.Text = _config.OutputFilePath;
        TxtFilenamePattern.Text = _config.OutputFileFilenamePattern;
        SldJpegQuality.Value = _config.OutputFileJpegQuality;
        TxtJpegQuality.Text = _config.OutputFileJpegQuality.ToString();
        ChkCopyPathToClipboard.IsChecked = _config.OutputFileCopyPathToClipboard;

        // Format selection
        int formatIdx = _config.OutputFileFormat switch
        {
            OutputFormat.Jpg  => 1,
            OutputFormat.Bmp  => 2,
            OutputFormat.Tiff => 3,
            _                 => 0
        };
        CmbFormat.SelectedIndex = formatIdx;

        // Hotkeys
        TxtRegionHotkey.Text = _config.RegionHotkey;
        TxtFullscreenHotkey.Text = _config.FullscreenHotkey;
        TxtWindowHotkey.Text = _config.WindowHotkey;
        TxtLastRegionHotkey.Text = _config.LastRegionHotkey;

        SldJpegQuality.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "Value")
                TxtJpegQuality.Text = ((int)SldJpegQuality.Value).ToString();
        };
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        _config.CaptureMousePointer = ChkMousePointer.IsChecked == true;
        _config.CaptureDelay = (int)(NudDelay.Value ?? 100);
        _config.ShowTrayNotification = ChkShowNotification.IsChecked == true;
        _config.PlayCameraSound = ChkPlaySound.IsChecked == true;

        _config.OutputFilePath = TxtOutputPath.Text ?? _config.OutputFilePath;
        _config.OutputFileFilenamePattern = TxtFilenamePattern.Text ?? _config.OutputFileFilenamePattern;
        _config.OutputFileJpegQuality = (int)SldJpegQuality.Value;
        _config.OutputFileCopyPathToClipboard = ChkCopyPathToClipboard.IsChecked == true;

        _config.OutputFileFormat = CmbFormat.SelectedIndex switch
        {
            1 => OutputFormat.Jpg,
            2 => OutputFormat.Bmp,
            3 => OutputFormat.Tiff,
            _ => OutputFormat.Png
        };

        _config.RegionHotkey = TxtRegionHotkey.Text ?? _config.RegionHotkey;
        _config.FullscreenHotkey = TxtFullscreenHotkey.Text ?? _config.FullscreenHotkey;
        _config.WindowHotkey = TxtWindowHotkey.Text ?? _config.WindowHotkey;
        _config.LastRegionHotkey = TxtLastRegionHotkey.Text ?? _config.LastRegionHotkey;

        if (_config is CoreConfiguration cfg) cfg.Save();
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private async void OnBrowseOutput(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select output folder",
            AllowMultiple = false
        });
        if (result.Count > 0)
            TxtOutputPath.Text = result[0].Path.LocalPath;
    }
}
