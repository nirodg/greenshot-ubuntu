using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Greenshot.Base.Platform;

public interface IScreenCaptureProvider
{
    bool IsAvailable { get; }

    Task<Image<Rgba32>?> CaptureFullScreenAsync(int screenIndex = -1, CancellationToken ct = default);
    Task<Image<Rgba32>?> CaptureRegionAsync(Rectangle region, CancellationToken ct = default);
    Task<Image<Rgba32>?> CaptureWindowAsync(nint windowHandle, CancellationToken ct = default);

    Rectangle GetScreenBounds(int screenIndex = -1);
    IEnumerable<Rectangle> GetAllScreenBounds();
    Point GetCursorPosition();

    /// <summary>
    /// Platform-native interactive capture (e.g. Wayland portal with interactive=true).
    /// The compositor handles region/window selection and returns the cropped image.
    /// Returns null if not supported on this platform — callers should fall back to
    /// full-screen capture + overlay.
    /// </summary>
    Task<Image<Rgba32>?> CaptureInteractiveAsync(CancellationToken ct = default)
        => Task.FromResult<Image<Rgba32>?>(null);
}
