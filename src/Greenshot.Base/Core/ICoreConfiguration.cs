using Greenshot.Base.Core.Enums;

namespace Greenshot.Base.Core;

public interface ICoreConfiguration
{
    string Language { get; set; }

    string RegionHotkey { get; set; }
    string WindowHotkey { get; set; }
    string FullscreenHotkey { get; set; }
    string LastRegionHotkey { get; set; }
    string ClipboardHotkey { get; set; }

    bool IsFirstLaunch { get; set; }

    List<string> OutputDestinations { get; set; }

    bool CaptureMousePointer { get; set; }
    int CaptureDelay { get; set; }

    string OutputFilePath { get; set; }
    string OutputFileFilenamePattern { get; set; }
    OutputFormat OutputFileFormat { get; set; }
    int OutputFileJpegQuality { get; set; }
    bool OutputFileCopyPathToClipboard { get; set; }
    bool OutputFileAllowOverwrite { get; set; }
    uint OutputFileIncrementingNumber { get; set; }

    bool PlayCameraSound { get; set; }
    bool ShowTrayNotification { get; set; }

    bool ZoomerEnabled { get; set; }
    float ZoomerOpacity { get; set; }

    int UpdateCheckInterval { get; set; }
    DateTime LastUpdateCheck { get; set; }

    bool DisableSettings { get; set; }
    bool HideTrayIcon { get; set; }
}
