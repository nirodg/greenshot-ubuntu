using Avalonia.Media;
using SkiaSharp;

namespace Greenshot.Editor.Drawing;

public class RectangleContainer : DrawableContainer
{
    public override void Draw(SKCanvas canvas, double scale)
    {
        var rect = GetSkRect(scale);

        if (FillColor != Colors.Transparent)
        {
            using var fill = CreateFillPaint();
            canvas.DrawRect(rect, fill);
        }

        if (Shadow)
        {
            using var shadow = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = new SKColor(0, 0, 0, 80),
                StrokeWidth = (float)(LineThickness * scale) + 2,
                IsAntialias = true,
                ImageFilter = SKImageFilter.CreateDropShadow(2, 2, 2, 2, SKColors.Black)
            };
            canvas.DrawRect(rect, shadow);
        }

        using var line = CreateLinePaint(scale);
        canvas.DrawRect(rect, line);
    }
}
