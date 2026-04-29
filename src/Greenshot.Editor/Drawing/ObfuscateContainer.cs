using SkiaSharp;

namespace Greenshot.Editor.Drawing;

public class ObfuscateContainer : DrawableContainer
{
    public int PixelSize { get; set; } = 8;

    private SKBitmap? _capturedRegion;

    public void SetCapturedRegion(SKBitmap? region)
    {
        _capturedRegion?.Dispose();
        _capturedRegion = region;
    }

    public override void Draw(SKCanvas canvas, double scale)
    {
        var rect = GetSkRect(scale);

        if (_capturedRegion != null)
        {
            // Pixelate: scale down to 1/PixelSize then back up (no filtering = blocky pixels)
            int smallW = Math.Max(1, (int)(Bounds.Width / PixelSize));
            int smallH = Math.Max(1, (int)(Bounds.Height / PixelSize));

            using var small = _capturedRegion.Resize(new SKImageInfo(smallW, smallH), SKFilterQuality.None);
            if (small != null)
            {
                using var paint = new SKPaint { FilterQuality = SKFilterQuality.None };
                canvas.DrawBitmap(small, rect, paint);
            }
        }
        else
        {
            // Fallback when background hasn't been captured yet: frosted-glass look
            using var blurPaint = new SKPaint
            {
                ImageFilter = SKImageFilter.CreateBlur(8f * (float)scale, 8f * (float)scale),
                Color = SKColors.White.WithAlpha(100)
            };
            canvas.DrawRect(rect, blurPaint);
            using var overlayPaint = new SKPaint
            {
                Color = new SKColor(100, 100, 100, 120)
            };
            canvas.DrawRect(rect, overlayPaint);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _capturedRegion?.Dispose();
    }
}
