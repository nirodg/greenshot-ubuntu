using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Greenshot.Base.Core;

public class Capture : ICapture
{
    public ICaptureDetails CaptureDetails { get; set; } = new CaptureDetails();
    public Image<Rgba32>? Image { get; set; }
    public Rectangle ScreenBounds { get; set; }
    public Point Location { get; set; }
    public bool HasCursor { get; set; }
    public Point CursorLocation { get; set; }

    public bool Crop(Rectangle cropRectangle)
    {
        if (Image == null) return false;
        if (cropRectangle.Width <= 0 || cropRectangle.Height <= 0) return false;
        if (!new Rectangle(0, 0, Image.Width, Image.Height).Contains(cropRectangle)) return false;

        var cropped = Image.Clone(ctx => { ctx.Crop(new SixLabors.ImageSharp.Rectangle(
            cropRectangle.X, cropRectangle.Y, cropRectangle.Width, cropRectangle.Height)); });
        Image.Dispose();
        Image = cropped;
        Location = new Point(Location.X + cropRectangle.X, Location.Y + cropRectangle.Y);
        return true;
    }

    public void Dispose()
    {
        Image?.Dispose();
        Image = null;
    }
}
