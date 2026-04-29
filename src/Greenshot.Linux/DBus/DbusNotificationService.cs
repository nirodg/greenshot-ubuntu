using System.Diagnostics;
using Greenshot.Base.Platform;
using Microsoft.Extensions.Logging;

namespace Greenshot.Linux.DBus;

public class DbusNotificationService : INotificationService
{
    private readonly ILogger<DbusNotificationService> _logger;

    public DbusNotificationService(ILogger<DbusNotificationService> logger)
    {
        _logger = logger;
    }

    public async Task ShowNotificationAsync(string title, string message, string? iconPath = null)
    {
        try
        {
            var icon = iconPath ?? "camera-photo";
            var args = $"-a Greenshot -i {EscapeShell(icon)} {EscapeShell(title)} {EscapeShell(message)}";

            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo("notify-send", args)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
                _logger.LogDebug("notify-send exited with code {Code}", proc.ExitCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show notification");
        }
    }

    private static string EscapeShell(string s) => $"'{s.Replace("'", "'\\''")}'";
}
