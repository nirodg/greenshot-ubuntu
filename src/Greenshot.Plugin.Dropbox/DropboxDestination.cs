using System.Net.Http.Headers;
using System.Text.Json;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces;
using Microsoft.Extensions.Logging;

namespace Greenshot.Plugin.Dropbox;

public class DropboxDestination : IDestination
{
    private readonly DropboxConfiguration _config;
    private readonly ILogger<DropboxDestination> _logger;
    private static readonly HttpClient HttpClient = new();

    private const string DropboxUploadUrl = "https://content.dropboxapi.com/2/files/upload";

    public DropboxDestination(DropboxConfiguration config, ILogger<DropboxDestination> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string Name => "Dropbox";
    public string Description => "Upload to Dropbox";
    public int Priority => 6;
    public bool IsActive => !string.IsNullOrEmpty(_config.AccessToken);

    public async Task<ExportInformation> ExportCaptureAsync(
        bool manuallyInitiated, ICapture capture, CancellationToken ct = default)
    {
        if (capture.Image == null) return new ExportInformation(false, ErrorMessage: "No image");
        if (string.IsNullOrEmpty(_config.AccessToken))
            return new ExportInformation(false, ErrorMessage: "Dropbox access token not configured");

        try
        {
            var bytes = await ImageSaveHelper.ToBytesAsync(capture.Image, OutputFormat.Png);
            var filename = FilenameHelper.FillPattern(
                "${capturetime:yyyy-MM-dd_HH-mm-ss}-${title}", capture.CaptureDetails) + ".png";
            var remotePath = $"{_config.UploadPath.TrimEnd('/')}/{filename}";

            var apiArg = JsonSerializer.Serialize(new
            {
                path = remotePath,
                mode = "add",
                autorename = true,
                mute = false
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, DropboxUploadUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.AccessToken);
            request.Headers.Add("Dropbox-API-Arg", apiArg);
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await HttpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var shareLink = doc.RootElement.TryGetProperty("path_display", out var path) ? path.GetString() : remotePath;

            _logger.LogInformation("Uploaded to Dropbox: {Path}", shareLink);
            return new ExportInformation(true, Url: $"dropbox:{shareLink}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dropbox upload failed");
            return new ExportInformation(false, ErrorMessage: ex.Message);
        }
    }
}
