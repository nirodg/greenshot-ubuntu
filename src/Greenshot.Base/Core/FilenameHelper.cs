namespace Greenshot.Base.Core;

public static class FilenameHelper
{
    public static string FillPattern(string pattern, ICaptureDetails details)
    {
        var now = details.DateTime;

        return pattern
            .Replace("${title}", SanitizeFilename(details.Title))
            .Replace("${capturetime:yyyy-MM-dd_HH-mm-ss}", now.ToString("yyyy-MM-dd_HH-mm-ss"))
            .Replace("${capturetime:d\"yyyy-MM-dd HH_mm_ss\"}", now.ToString("yyyy-MM-dd HH_mm_ss"))
            .Replace("${capturetime}", now.ToString("yyyy-MM-dd_HH-mm-ss"))
            .Replace("${NUM}", details is CaptureDetails cd ? cd.MetaData.GetValueOrDefault("NUM", "1") : "1");
    }

    private static string SanitizeFilename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "screenshot";
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim('_', ' ');
    }

    public static string GetUniqueFilename(string directory, string baseName, string extension)
    {
        var path = Path.Combine(directory, baseName + extension);
        if (!File.Exists(path)) return path;

        int i = 1;
        while (true)
        {
            path = Path.Combine(directory, $"{baseName}_{i}{extension}");
            if (!File.Exists(path)) return path;
            i++;
        }
    }
}
