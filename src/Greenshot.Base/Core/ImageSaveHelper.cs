using Greenshot.Base.Core.Enums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;

namespace Greenshot.Base.Core;

public static class ImageSaveHelper
{
    public static string GetExtension(OutputFormat format) => format switch
    {
        OutputFormat.Bmp => ".bmp",
        OutputFormat.Gif => ".gif",
        OutputFormat.Jpg => ".jpg",
        OutputFormat.Tiff => ".tiff",
        _ => ".png"
    };

    public static async Task SaveAsync(Image<Rgba32> image, string path, OutputFormat format, int jpegQuality = 80)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        switch (format)
        {
            case OutputFormat.Jpg:
                await image.SaveAsJpegAsync(path, new JpegEncoder { Quality = jpegQuality });
                break;
            case OutputFormat.Bmp:
                await image.SaveAsBmpAsync(path, new BmpEncoder());
                break;
            case OutputFormat.Gif:
                await image.SaveAsGifAsync(path, new GifEncoder());
                break;
            case OutputFormat.Tiff:
                await image.SaveAsTiffAsync(path, new TiffEncoder());
                break;
            default:
                await image.SaveAsPngAsync(path, new PngEncoder());
                break;
        }
    }

    public static async Task<byte[]> ToBytesAsync(Image<Rgba32> image, OutputFormat format, int jpegQuality = 80)
    {
        using var ms = new MemoryStream();
        switch (format)
        {
            case OutputFormat.Jpg:
                await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = jpegQuality });
                break;
            case OutputFormat.Bmp:
                await image.SaveAsBmpAsync(ms, new BmpEncoder());
                break;
            default:
                await image.SaveAsPngAsync(ms, new PngEncoder());
                break;
        }
        return ms.ToArray();
    }
}
