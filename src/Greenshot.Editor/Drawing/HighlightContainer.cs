using Avalonia.Media;
using SkiaSharp;

namespace Greenshot.Editor.Drawing;

public class HighlightContainer : DrawableContainer
{
    public HighlightContainer()
    {
        FillColor = Color.FromArgb(128, 255, 255, 0); // semi-transparent yellow
        LineThickness = 0;
    }

    public override void Draw(SKCanvas canvas, double scale)
    {
        var rect = GetSkRect(scale);
        using var paint = CreateFillPaint();
        canvas.DrawRect(rect, paint);
    }
}
