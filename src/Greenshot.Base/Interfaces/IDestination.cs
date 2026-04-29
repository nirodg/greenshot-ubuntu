using Greenshot.Base.Core;

namespace Greenshot.Base.Interfaces;

public interface IDestination
{
    string Name { get; }
    string Description { get; }
    int Priority { get; }
    bool IsActive { get; }

    Task<ExportInformation> ExportCaptureAsync(bool manuallyInitiated, ICapture capture, CancellationToken cancellationToken = default);
}

public record ExportInformation(bool Exported, string? FilePath = null, string? Url = null, string? ErrorMessage = null);
