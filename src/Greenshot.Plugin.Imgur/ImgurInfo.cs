using System.Text.Json.Serialization;

namespace Greenshot.Plugin.Imgur;

public class ImgurInfo
{
    [JsonPropertyName("id")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("deletehash")]
    public string DeleteHash { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonIgnore]
    public string PageLink => $"https://imgur.com/{Hash}";

    [JsonIgnore]
    public string SmallSquare => $"https://i.imgur.com/{Hash}s.jpg";
}

public class ImgurUploadResponse
{
    [JsonPropertyName("data")]
    public ImgurInfo? Data { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }
}
