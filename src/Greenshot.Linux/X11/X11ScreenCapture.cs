using Greenshot.Base.Platform;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;

namespace Greenshot.Linux.X11;

public class X11ScreenCapture : IScreenCaptureProvider, IDisposable
{
    private readonly ILogger<X11ScreenCapture> _logger;
    private readonly nint _display;
    private readonly int _screen;
    private readonly nint _root;

    public bool IsAvailable => _display != nint.Zero;

    public X11ScreenCapture(ILogger<X11ScreenCapture> logger)
    {
        _logger = logger;
        try
        {
            var displayEnv = Environment.GetEnvironmentVariable("DISPLAY");
            _logger.LogInformation("Initializing X11 screen capture. DISPLAY={Display}", displayEnv ?? "(not set)");
            _display = X11Api.XOpenDisplay(null);
            if (_display == nint.Zero)
            {
                _logger.LogWarning("Cannot open X11 display. DISPLAY={Display}. Running under Wayland without XWayland?", displayEnv ?? "(not set)");
                return;
            }
            _screen = X11Api.XDefaultScreen(_display);
            _root = X11Api.XDefaultRootWindow(_display);
            _logger.LogInformation("X11 display opened. Screen={Screen} Root={Root}", _screen, _root);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize X11 screen capture");
        }
    }

    public Task<Image<Rgba32>?> CaptureFullScreenAsync(int screenIndex = -1, CancellationToken ct = default)
    {
        var bounds = GetScreenBounds(screenIndex);
        return CaptureRegionAsync(bounds, ct);
    }

    public unsafe Task<Image<Rgba32>?> CaptureRegionAsync(Rectangle region, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("CaptureRegionAsync: display not available");
            return Task.FromResult<Image<Rgba32>?>(null);
        }

        _logger.LogInformation("Capturing region {Region}", region);

        try
        {
            X11Api.XSync(_display, false);

            var ximg = X11Api.XGetImage(_display, _root,
                region.X, region.Y, (uint)region.Width, (uint)region.Height,
                X11Api.AllPlanes, X11Api.ZPixmap);

            if (ximg == null)
            {
                _logger.LogWarning("XGetImage returned null — falling back to Wayland capture");
                return CaptureViaWaylandFallbackAsync(region, ct);
            }

            var result = ConvertXImageToImageSharp(ximg);
            X11Api.XDestroyImage(ximg);

            return Task.FromResult<Image<Rgba32>?>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture region {Region}", region);
            return Task.FromResult<Image<Rgba32>?>(null);
        }
    }

    /// <summary>
    /// On Wayland, GNOME 46+ denies silent (interactive=false) screenshots for non-sandboxed apps.
    /// This method uses interactive=true so GNOME shows its native screenshot UI and the user
    /// selects the area/window/screen.  Returns null on pure X11 (let caller use the overlay).
    /// </summary>
    public Task<Image<Rgba32>?> CaptureInteractiveAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            return Task.FromResult<Image<Rgba32>?>(null); // X11: caller uses overlay instead

        _logger.LogInformation("Using XDG portal interactive capture (GNOME handles selection)");
        var portal = new Greenshot.Linux.DBus.XdgScreenshotPortal(_logger);
        return portal.TakeScreenshotAsync(interactive: true, ct);
    }

    public Task<Image<Rgba32>?> CaptureWindowAsync(nint windowHandle, CancellationToken ct = default)
    {
        if (!IsAvailable) return Task.FromResult<Image<Rgba32>?>(null);

        try
        {
            X11Api.XGetWindowAttributes(_display, windowHandle, out var attrs);
            var region = new Rectangle(attrs.x, attrs.y, attrs.width, attrs.height);
            return CaptureRegionAsync(region, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture window {Handle}", windowHandle);
            return Task.FromResult<Image<Rgba32>?>(null);
        }
    }

    public Rectangle GetScreenBounds(int screenIndex = -1)
    {
        if (!IsAvailable) return Rectangle.Empty;
        int w = X11Api.XDisplayWidth(_display, _screen);
        int h = X11Api.XDisplayHeight(_display, _screen);
        return new Rectangle(0, 0, w, h);
    }

    public IEnumerable<Rectangle> GetAllScreenBounds()
    {
        yield return GetScreenBounds();
    }

    public Point GetCursorPosition()
    {
        if (!IsAvailable) return Point.Empty;
        X11Api.XQueryPointer(_display, _root,
            out _, out _, out int rootX, out int rootY,
            out _, out _, out _);
        return new Point(rootX, rootY);
    }

    // ---------------------------------------------------------------
    // Wayland fallback capture (GNOME Shell D-Bus → grim)
    // ---------------------------------------------------------------

    private async Task<Image<Rgba32>?> CaptureViaWaylandFallbackAsync(Rectangle region, CancellationToken ct)
    {
        // Try XDG Desktop Portal silent capture (interactive=false).
        // NOTE: GNOME 46+ denies this for non-sandboxed apps — callers should use
        // CaptureInteractiveAsync() which uses interactive=true instead.
        var portal = new Greenshot.Linux.DBus.XdgScreenshotPortal(_logger);
        var fullScreen = await portal.TakeScreenshotAsync(interactive: false, ct);
        if (fullScreen != null)
        {
            // Crop to the requested region if needed.
            var screenBounds = GetScreenBounds();
            bool isFullScreen = region.X == 0 && region.Y == 0
                && region.Width == fullScreen.Width && region.Height == fullScreen.Height;

            if (!isFullScreen)
            {
                fullScreen.Mutate(ctx => ctx.Crop(new Rectangle(
                    region.X, region.Y, region.Width, region.Height)));
            }
            return fullScreen;
        }

        // Fall back to grim (wlroots/sway).
        var result = await TryCaptureViaGrimAsync(region, ct);
        if (result != null) return result;

        _logger.LogError("All Wayland capture methods failed for region {Region}", region);
        return null;
    }

    private async Task<Image<Rgba32>?> TryCaptureViaGrimAsync(Rectangle region, CancellationToken ct)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"greenshot_{Guid.NewGuid():N}.png");
        try
        {
            // grim geometry format: "X,Y WxH"
            var geometry = $"{region.X},{region.Y} {region.Width}x{region.Height}";
            var psi = new ProcessStartInfo
            {
                FileName = "grim",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-g");
            psi.ArgumentList.Add(geometry);
            psi.ArgumentList.Add(tempFile);

            using var proc = new Process { StartInfo = psi };
            proc.Start();
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0 || !File.Exists(tempFile))
            {
                _logger.LogDebug("grim screenshot failed (exit {Code})", proc.ExitCode);
                return null;
            }

            _logger.LogInformation("Captured via grim");
            return await Image.LoadAsync<Rgba32>(tempFile, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "grim screenshot failed");
            return null;
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    private static unsafe Image<Rgba32> ConvertXImageToImageSharp(XImage* ximg)
    {
        int width = ximg->width;
        int height = ximg->height;
        int bpp = ximg->bits_per_pixel;
        int bytesPerLine = ximg->bytes_per_line;

        var image = new Image<Rgba32>(width, height);

        for (int y = 0; y < height; y++)
        {
            var row = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            byte* srcRow = ximg->data + y * bytesPerLine;

            for (int x = 0; x < width; x++)
            {
                uint pixel;
                if (bpp == 32)
                {
                    pixel = *(uint*)(srcRow + x * 4);
                }
                else if (bpp == 24)
                {
                    var p = srcRow + x * 3;
                    pixel = (uint)(p[0] | (p[1] << 8) | (p[2] << 16));
                }
                else
                {
                    pixel = 0;
                }

                // X11 typically stores as BGRA or BGRX
                byte b = (byte)(pixel & 0xFF);
                byte g = (byte)((pixel >> 8) & 0xFF);
                byte r = (byte)((pixel >> 16) & 0xFF);
                byte a = bpp == 32 ? (byte)((pixel >> 24) & 0xFF) : (byte)255;
                if (a == 0) a = 255; // BGRX has alpha=0, treat as opaque

                row[x] = new Rgba32(r, g, b, a);
            }
        }

        return image;
    }

    ~X11ScreenCapture() => Dispose();

    public void Dispose()
    {
        if (_display != nint.Zero)
            X11Api.XCloseDisplay(_display);
        GC.SuppressFinalize(this);
    }
}
