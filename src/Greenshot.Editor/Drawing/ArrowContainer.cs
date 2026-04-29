using Avalonia;
using SkiaSharp;

namespace Greenshot.Editor.Drawing;

public class ArrowContainer : DrawableContainer
{
    public bool ArrowAtStart { get; set; } = false;
    public bool ArrowAtEnd { get; set; } = true;
    public float ArrowSize { get; set; } = 12f;

    public override void Draw(SKCanvas canvas, double scale)
    {
        using var paint = CreateLinePaint(scale);

        float x1 = (float)(Bounds.X * scale);
        float y1 = (float)(Bounds.Y * scale);
        float x2 = (float)((Bounds.X + Bounds.Width) * scale);
        float y2 = (float)((Bounds.Y + Bounds.Height) * scale);

        canvas.DrawLine(x1, y1, x2, y2, paint);

        if (ArrowAtEnd)
            DrawArrowhead(canvas, paint, x1, y1, x2, y2, scale);
        if (ArrowAtStart)
            DrawArrowhead(canvas, paint, x2, y2, x1, y1, scale);
    }

    private void DrawArrowhead(SKCanvas canvas, SKPaint paint, float fromX, float fromY, float toX, float toY, double scale)
    {
        double angle = Math.Atan2(toY - fromY, toX - fromX);
        // Arrowhead scales with both view scale and line thickness
        float size = (float)(Math.Max(ArrowSize, LineThickness * 3.0) * scale);
        float spread = (float)(Math.PI / 6); // 30 degrees

        float ax1 = toX - size * (float)Math.Cos(angle - spread);
        float ay1 = toY - size * (float)Math.Sin(angle - spread);
        float ax2 = toX - size * (float)Math.Cos(angle + spread);
        float ay2 = toY - size * (float)Math.Sin(angle + spread);

        var path = new SKPath();
        path.MoveTo(toX, toY);
        path.LineTo(ax1, ay1);
        path.LineTo(ax2, ay2);
        path.Close();

        using var fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = paint.Color,
            IsAntialias = true
        };
        canvas.DrawPath(path, fillPaint);
    }
}
