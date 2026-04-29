using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Platform;
using Greenshot.Views;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Greenshot.Helpers;

public class CaptureHelper
{
    private readonly IScreenCaptureProvider _captureProvider;
    private readonly ICoreConfiguration _config;
    private readonly ILogger<CaptureHelper> _logger;

    private Rectangle? _lastRegion;

    public CaptureHelper(
        IScreenCaptureProvider captureProvider,
        ICoreConfiguration config,
        ILogger<CaptureHelper> logger)
    {
        _captureProvider = captureProvider;
        _config = config;
        _logger = logger;
    }

    public async Task<ICapture?> CaptureFullScreenAsync(CancellationToken ct = default)
    {
        await Task.Delay(_config.CaptureDelay, ct);
        _logger.LogDebug("Capturing full screen");

        var img = await _captureProvider.CaptureFullScreenAsync(-1, ct);
        if (img == null) return null;

        var bounds = _captureProvider.GetScreenBounds();
        return BuildCapture(img, bounds, CaptureMode.FullScreen, "Full Screen");
    }

    public async Task<ICapture?> CaptureRegionInteractiveAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Capturing region interactively");

        // On Wayland, use the platform's native interactive capture (XDG portal with interactive=true).
        // GNOME shows its own screenshot UI; the user selects a region/window/screen.
        // Returns null on X11 — we fall through to full-screen + overlay.
        var nativeImg = await _captureProvider.CaptureInteractiveAsync(ct);
        if (nativeImg != null)
        {
            _logger.LogInformation("Native interactive capture succeeded ({W}x{H})", nativeImg.Width, nativeImg.Height);
            var nb = new Rectangle(0, 0, nativeImg.Width, nativeImg.Height);
            return BuildCapture(nativeImg, nb, CaptureMode.Region, "Region");
        }

        var screenImg = await _captureProvider.CaptureFullScreenAsync(-1, ct);
        if (screenImg == null)
        {
            _logger.LogError("Screen capture returned null — no X11 display and no Wayland portal available");
            return null;
        }

        var selected = await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var overlay = new CaptureOverlayWindow(screenImg);
            overlay.Show();
            return await overlay.WaitForSelectionAsync();
        });

        if (!selected.HasValue || selected.Value.Width < 3 || selected.Value.Height < 3)
        {
            _logger.LogInformation("Region capture cancelled or too small");
            screenImg.Dispose();
            return null;
        }

        _lastRegion = selected.Value;

        try
        {
            var cropped = screenImg.Clone(ctx => { ctx.Crop(new SixLabors.ImageSharp.Rectangle(
                selected.Value.X, selected.Value.Y,
                selected.Value.Width, selected.Value.Height)); });
            return BuildCapture(cropped, selected.Value, CaptureMode.Region, "Region");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crop captured image");
            return null;
        }
        finally
        {
            screenImg.Dispose();
        }
    }

    public async Task<ICapture?> CaptureLastRegionAsync(CancellationToken ct = default)
    {
        if (!_lastRegion.HasValue) return await CaptureRegionInteractiveAsync(ct);

        await Task.Delay(_config.CaptureDelay, ct);
        var img = await _captureProvider.CaptureRegionAsync(_lastRegion.Value, ct);
        if (img == null) return null;

        return BuildCapture(img, _lastRegion.Value, CaptureMode.LastRegion, "Last Region");
    }

    public async Task<ICapture?> CaptureWindowInteractiveAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Capturing window interactively");

        // On Wayland, use the platform's native interactive capture.
        var nativeImg = await _captureProvider.CaptureInteractiveAsync(ct);
        if (nativeImg != null)
        {
            _logger.LogInformation("Native interactive capture succeeded ({W}x{H})", nativeImg.Width, nativeImg.Height);
            var nb = new Rectangle(0, 0, nativeImg.Width, nativeImg.Height);
            return BuildCapture(nativeImg, nb, CaptureMode.Window, "Window");
        }

        var screenImg = await _captureProvider.CaptureFullScreenAsync(-1, ct);
        if (screenImg == null)
        {
            _logger.LogError("Screen capture returned null — no X11 display and no Wayland portal available");
            return null;
        }

        var selected = await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var overlay = new CaptureOverlayWindow(screenImg) { ShowWindowHighlight = true };
            overlay.Show();
            return await overlay.WaitForSelectionAsync();
        });

        if (!selected.HasValue || selected.Value.Width < 3 || selected.Value.Height < 3)
        {
            _logger.LogInformation("Window capture cancelled or too small");
            screenImg.Dispose();
            return null;
        }

        try
        {
            var cropped = screenImg.Clone(ctx => { ctx.Crop(new SixLabors.ImageSharp.Rectangle(
                selected.Value.X, selected.Value.Y,
                selected.Value.Width, selected.Value.Height)); });
            return BuildCapture(cropped, selected.Value, CaptureMode.Window, "Window");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crop captured image");
            return null;
        }
        finally
        {
            screenImg.Dispose();
        }
    }

    private static ICapture BuildCapture(Image<Rgba32> img, Rectangle bounds, CaptureMode mode, string title)
    {
        return new Capture
        {
            Image = img,
            ScreenBounds = bounds,
            Location = bounds.Location,
            CaptureDetails = new CaptureDetails
            {
                Title = title,
                DateTime = DateTime.Now,
                CaptureMode = mode
            }
        };
    }
}
