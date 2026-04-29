using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tmds.DBus.Protocol;

namespace Greenshot.Linux.DBus;

/// <summary>
/// Takes a screenshot via the XDG Desktop Portal (org.freedesktop.portal.Screenshot).
/// Works on all modern Wayland compositors: GNOME (xdg-desktop-portal-gnome),
/// KDE Plasma (xdg-desktop-portal-kde), sway/wlroots (xdg-desktop-portal-wlr), etc.
/// </summary>
internal sealed class XdgScreenshotPortal
{
    private readonly ILogger _logger;

    public XdgScreenshotPortal(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// interactive=false: silent full-screen capture (may be denied by GNOME 46+ for non-sandboxed apps).
    /// interactive=true:  GNOME shows its native screenshot UI — user selects region/window/screen.
    /// </summary>
    public async Task<Image<Rgba32>?> TakeScreenshotAsync(bool interactive, CancellationToken ct)
    {
        using var connection = new Connection(Address.Session!);
        try
        {
            await connection.ConnectAsync();

            // Build the request handle path from our unique bus name + a random token.
            // We MUST subscribe to the Response signal on this path BEFORE calling
            // Screenshot to avoid a race condition.
            var token = $"gs{Random.Shared.Next(100_000, 999_999)}";
            var senderEscaped = connection.UniqueName!.TrimStart(':').Replace('.', '_');
            var requestPath = $"/org/freedesktop/portal/desktop/request/{senderEscaped}/{token}";

            _logger.LogDebug("XDG portal request path: {Path} interactive={Interactive}", requestPath, interactive);

            var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Subscribe to the Response signal first.
            var matchRule = new MatchRule
            {
                Type      = MessageType.Signal,
                Path      = requestPath,
                Interface = "org.freedesktop.portal.Request",
                Member    = "Response"
            };

            await connection.AddMatchAsync<string?>(
                matchRule,
                // Read the signal body: (uint response, a{sv} results)
                // Pass logger as readerState so we can log the response code.
                static (message, readerState) =>
                {
                    var log = (ILogger)readerState!;
                    var reader = message.GetBodyReader();
                    uint code = reader.ReadUInt32();
                    log.LogDebug("XDG portal Response code: {Code} ({Meaning})",
                        code,
                        code == 0 ? "success" : code == 1 ? "user-cancelled" : "error");
                    if (code != 0) return null;
                    var dict = reader.ReadDictionaryOfStringToVariantValue();
                    if (!dict.TryGetValue("uri", out var uriVal))
                    {
                        log.LogWarning("XDG portal Response code=0 but no 'uri' key in results");
                        return null;
                    }
                    return uriVal.GetString();
                },
                // Handler: complete the TCS with the URI (or null).
                static (ex, val, _, handlerState) =>
                {
                    var t = (TaskCompletionSource<string?>)handlerState!;
                    if (ex != null) t.TrySetException(ex);
                    else           t.TrySetResult(val);
                },
                readerState: _logger,
                handlerState: tcs,
                emitOnCapturedContext: false,
                subscribe: true);

            // Build and send the Screenshot call.
            var buffer = BuildScreenshotMessage(connection, token, interactive);
            await connection.CallMethodAsync(buffer);

            // Now wait for the Response signal (up to 30 s).
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(30));

            var fileUri = await tcs.Task.WaitAsync(linked.Token);
            if (string.IsNullOrEmpty(fileUri))
            {
                _logger.LogWarning("XDG portal screenshot cancelled or denied");
                return null;
            }

            var filePath = new Uri(fileUri).LocalPath;
            _logger.LogInformation("XDG portal screenshot: {Path}", filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("XDG portal screenshot file not found: {Path}", filePath);
                return null;
            }

            try
            {
                return await Image.LoadAsync<Rgba32>(filePath, ct);
            }
            finally
            {
                try { File.Delete(filePath); } catch { /* best-effort cleanup */ }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("XDG portal screenshot timed out or was cancelled (interactive={Interactive})", interactive);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "XDG portal screenshot call failed (interactive={Interactive})", interactive);
            return null;
        }
    }

    // Synchronous helper — MessageWriter/ArrayStart are ref structs and cannot
    // be used across await points in an async method.
    private static MessageBuffer BuildScreenshotMessage(Connection connection, string token, bool interactive)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            "org.freedesktop.portal.Desktop",
            "/org/freedesktop/portal/desktop",
            "org.freedesktop.portal.Screenshot",
            "Screenshot",
            "sa{sv}",
            MessageFlags.None);

        writer.WriteString("");   // parent_window (empty = use root)

        // a{sv} options
        var dictStart = writer.WriteDictionaryStart();
        writer.WriteDictionaryEntryStart();
        writer.WriteString("handle_token");
        writer.WriteVariantString(token);
        writer.WriteDictionaryEntryStart();
        writer.WriteString("interactive");
        writer.WriteVariantBool(interactive);
        writer.WriteDictionaryEnd(dictStart);

        var buffer = writer.CreateMessage();
        writer.Dispose();
        return buffer;
    }
}
