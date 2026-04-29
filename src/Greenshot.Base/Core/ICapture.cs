using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Greenshot.Base.Core;

public interface ICapture : IDisposable
{
    ICaptureDetails CaptureDetails { get; set; }
    Image<Rgba32>? Image { get; set; }
    Rectangle ScreenBounds { get; set; }
    Point Location { get; set; }
    bool HasCursor { get; set; }
    Point CursorLocation { get; set; }

    bool Crop(Rectangle cropRectangle);
}
