using System.Diagnostics;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces;

namespace Greenshot.Destinations;

public class ClipboardDestination : IDestination
{
    public string Name => "Clipboard";
    public string Description => "Copy to clipboard";
    public int Priority => 1;
    public bool IsActive => true;

    public async Task<ExportInformation> ExportCaptureAsync(
        bool manuallyInitiated, ICapture capture, CancellationToken ct = default)
    {
        if (capture.Image == null)
            return new ExportInformation(false, ErrorMessage: "No image");

        try
        {
            var bytes = await ImageSaveHelper.ToBytesAsync(capture.Image, OutputFormat.Png);

            // Use xclip or xsel to copy to clipboard on Linux
            if (await TryCopyWithXclipAsync(bytes, ct)) return new ExportInformation(true);
            if (await TryCopyWithXselAsync(bytes, ct)) return new ExportInformation(true);
            if (await TryCopyWithWlCopyAsync(bytes, ct)) return new ExportInformation(true);

            return new ExportInformation(false, ErrorMessage: "No clipboard tool found (install xclip, xsel, or wl-clipboard)");
        }
        catch (Exception ex)
        {
            return new ExportInformation(false, ErrorMessage: ex.Message);
        }
    }

    private static async Task<bool> TryCopyWithXclipAsync(byte[] data, CancellationToken ct)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo("xclip",
                    "-selection clipboard -t image/png")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            await proc.StandardInput.BaseStream.WriteAsync(data, ct);
            proc.StandardInput.Close();
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task<bool> TryCopyWithXselAsync(byte[] data, CancellationToken ct)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo("xsel",
                    "--clipboard --input")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            await proc.StandardInput.BaseStream.WriteAsync(data, ct);
            proc.StandardInput.Close();
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task<bool> TryCopyWithWlCopyAsync(byte[] data, CancellationToken ct)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo("wl-copy",
                    "--type image/png")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            await proc.StandardInput.BaseStream.WriteAsync(data, ct);
            proc.StandardInput.Close();
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }
}
