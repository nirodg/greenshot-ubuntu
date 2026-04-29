using System.Text.Json;
using Greenshot.Base.Core.Enums;

namespace Greenshot.Base.Core;

public class CoreConfiguration : ICoreConfiguration
{
    public string Language { get; set; } = "en-US";

    public string RegionHotkey { get; set; } = "Print";
    public string WindowHotkey { get; set; } = "Alt+Print";
    public string FullscreenHotkey { get; set; } = "Ctrl+Print";
    public string LastRegionHotkey { get; set; } = "Shift+Print";
    public string ClipboardHotkey { get; set; } = "Ctrl+Shift+Print";

    public bool IsFirstLaunch { get; set; } = true;

    public List<string> OutputDestinations { get; set; } = ["Picker"];

    public bool CaptureMousePointer { get; set; } = true;
    public int CaptureDelay { get; set; } = 100;

    public string OutputFilePath { get; set; } = GetDefaultOutputPath();
    public string OutputFileFilenamePattern { get; set; } = "${capturetime:yyyy-MM-dd_HH-mm-ss}-${title}";
    public OutputFormat OutputFileFormat { get; set; } = OutputFormat.Png;
    public int OutputFileJpegQuality { get; set; } = 80;
    public bool OutputFileCopyPathToClipboard { get; set; } = true;
    public bool OutputFileAllowOverwrite { get; set; } = true;
    public uint OutputFileIncrementingNumber { get; set; } = 1;

    public bool PlayCameraSound { get; set; } = false;
    public bool ShowTrayNotification { get; set; } = true;

    public bool ZoomerEnabled { get; set; } = true;
    public float ZoomerOpacity { get; set; } = 1.0f;

    public int UpdateCheckInterval { get; set; } = 14;
    public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;

    public bool DisableSettings { get; set; } = false;
    public bool HideTrayIcon { get; set; } = false;

    private static string GetDefaultOutputPath()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return string.IsNullOrEmpty(pictures)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures")
            : pictures;
    }

    private static string ConfigFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "greenshot", "greenshot.json");

    public static CoreConfiguration Load()
    {
        var path = ConfigFilePath;
        if (!File.Exists(path)) return new CoreConfiguration();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CoreConfiguration>(json) ?? new CoreConfiguration();
        }
        catch
        {
            return new CoreConfiguration();
        }
    }

    public void Save()
    {
        var path = ConfigFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
