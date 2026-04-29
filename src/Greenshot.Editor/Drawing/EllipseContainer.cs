using Avalonia.Media;
using SkiaSharp;

namespace Greenshot.Editor.Drawing;

public class EllipseContainer : DrawableContainer
{
    public override void Draw(SKCanvas canvas, double scale)
    {
        var rect = GetSkRect(scale);

        if (FillColor != Colors.Transparent)
        {
            using var fill = CreateFillPaint();
            canvas.DrawOval(rect, fill);
        }

        using var line = CreateLinePaint(scale);
        canvas.DrawOval(rect, line);
    }
}
