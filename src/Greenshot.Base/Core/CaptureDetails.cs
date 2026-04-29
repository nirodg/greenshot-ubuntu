using Greenshot.Base.Core.Enums;

namespace Greenshot.Base.Core;

public class CaptureDetails : ICaptureDetails
{
    public string Title { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public DateTime DateTime { get; set; } = DateTime.Now;
    public CaptureMode CaptureMode { get; set; } = CaptureMode.None;
    public string WindowHandle { get; set; } = string.Empty;
    public Dictionary<string, string> MetaData { get; } = new();

    public void AddMetaData(string key, string value)
    {
        MetaData[key] = value;
    }
}
