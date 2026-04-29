using Avalonia;
using Avalonia.Media;
using SkiaSharp;

namespace Greenshot.Editor.Drawing;

public abstract class DrawableContainer : IDisposable
{
    public Rect Bounds { get; set; }
    public bool Selected { get; set; }
    public bool Disposed { get; private set; }

    public Color LineColor { get; set; } = Colors.Red;
    public Color FillColor { get; set; } = Colors.Transparent;
    public int LineThickness { get; set; } = 2;
    public bool Shadow { get; set; } = true;

    protected DrawableContainer() { }

    public abstract void Draw(SKCanvas canvas, double scale);

    protected SKPaint CreateLinePaint(double scale)
    {
        var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(LineColor.R, LineColor.G, LineColor.B, LineColor.A),
            StrokeWidth = (float)(LineThickness * scale),
            IsAntialias = true
        };
        return paint;
    }

    protected SKPaint CreateFillPaint()
    {
        return new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = new SKColor(FillColor.R, FillColor.G, FillColor.B, FillColor.A),
            IsAntialias = true
        };
    }

    protected SKRect GetSkRect(double scale) =>
        new SKRect(
            (float)(Bounds.X * scale),
            (float)(Bounds.Y * scale),
            (float)((Bounds.X + Bounds.Width) * scale),
            (float)((Bounds.Y + Bounds.Height) * scale));

    public virtual bool HitTest(Point point) => Bounds.Contains(point);

    /// <summary>Called once when the user starts dragging this element.</summary>
    public virtual void BeginMove() { }

    /// <summary>Called repeatedly during drag with the target top-left position in image coordinates.</summary>
    public virtual void MoveTo(double x, double y)
    {
        Bounds = new Rect(x, y, Bounds.Width, Bounds.Height);
    }

    /// <summary>Called when the drag is finished.</summary>
    public virtual void EndMove() { }

    public virtual DrawableContainer Clone()
    {
        var clone = (DrawableContainer)MemberwiseClone();
        return clone;
    }

    public void Dispose()
    {
        Dispose(true);
        Disposed = true;
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) { }
}
