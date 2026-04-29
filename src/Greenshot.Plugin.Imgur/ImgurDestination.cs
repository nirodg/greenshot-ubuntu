using System.Net.Http.Headers;
using System.Text.Json;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces;
using Microsoft.Extensions.Logging;

namespace Greenshot.Plugin.Imgur;

public class ImgurDestination : IDestination
{
    private readonly ImgurConfiguration _config;
    private readonly ILogger<ImgurDestination> _logger;
    private static readonly HttpClient HttpClient = new();

    public ImgurDestination(ImgurConfiguration config, ILogger<ImgurDestination> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string Name => "Imgur";
    public string Description => "Upload to Imgur";
    public int Priority => 5;
    public bool IsActive => true;

    public async Task<ExportInformation> ExportCaptureAsync(
        bool manuallyInitiated, ICapture capture, CancellationToken ct = default)
    {
        if (capture.Image == null)
            return new ExportInformation(false, ErrorMessage: "No image");

        try
        {
            var bytes = await ImageSaveHelper.ToBytesAsync(capture.Image, OutputFormat.Png);
            var imgurInfo = await UploadToImgurAsync(bytes, capture.CaptureDetails.Title, ct);

            if (imgurInfo == null)
                return new ExportInformation(false, ErrorMessage: "Upload failed");

            _config.ImgurUploadHistory[imgurInfo.Hash] = imgurInfo.DeleteHash;
            _config.Save();

            var url = _config.UsePageLink ? imgurInfo.PageLink : imgurInfo.Link;
            _logger.LogInformation("Uploaded to Imgur: {Url}", url);

            return new ExportInformation(true, Url: url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Imgur upload failed");
            return new ExportInformation(false, ErrorMessage: ex.Message);
        }
    }

    private async Task<ImgurInfo?> UploadToImgurAsync(byte[] imageData, string title, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageData);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image");
        content.Add(new StringContent(title), "title");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.ImgurApi3Url}/upload");
        request.Content = content;

        // Anonymous upload — use the public Imgur API key
        // In production this should come from a credentials template
        request.Headers.Authorization = new AuthenticationHeaderValue("Client-ID", "YOUR_IMGUR_CLIENT_ID");

        var response = await HttpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<ImgurUploadResponse>(json);
        return result?.Data;
    }
}
