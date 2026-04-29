using System.Text.Json;

namespace Greenshot.Plugin.Dropbox;

public class DropboxConfiguration
{
    public string AccessToken { get; set; } = string.Empty;
    public string UploadPath { get; set; } = "/Screenshots";

    private static string ConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "greenshot", "dropbox.json");

    public static DropboxConfiguration Load()
    {
        if (!File.Exists(ConfigPath)) return new DropboxConfiguration();
        try { return JsonSerializer.Deserialize<DropboxConfiguration>(File.ReadAllText(ConfigPath)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
