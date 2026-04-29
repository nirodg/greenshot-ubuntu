using System.Text.Json;

namespace Greenshot.Plugin.Imgur;

public class ImgurConfiguration
{
    public string ImgurApi3Url { get; set; } = "https://api.imgur.com/3";
    public bool UsePageLink { get; set; } = false;
    public bool AnonymousAccess { get; set; } = true;
    public Dictionary<string, string> ImgurUploadHistory { get; set; } = new();

    private static string ConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "greenshot", "imgur.json");

    public static ImgurConfiguration Load()
    {
        if (!File.Exists(ConfigPath)) return new ImgurConfiguration();
        try
        {
            return JsonSerializer.Deserialize<ImgurConfiguration>(File.ReadAllText(ConfigPath))
                   ?? new ImgurConfiguration();
        }
        catch { return new ImgurConfiguration(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
