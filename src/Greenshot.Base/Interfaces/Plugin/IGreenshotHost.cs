using Greenshot.Base.Core;

namespace Greenshot.Base.Interfaces.Plugin;

public interface IGreenshotHost
{
    ICapture GetCapture(string? windowTitle = null);
    Task ExportCaptureAsync(ICapture capture);
    void NotifyCaptureTaken(ICapture capture);
}
