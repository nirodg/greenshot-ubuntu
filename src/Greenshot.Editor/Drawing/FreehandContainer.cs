using Avalonia;
using SkiaSharp;

namespace Greenshot.Editor.Drawing;

public class FreehandContainer : DrawableContainer
{
    private readonly List<Point> _points = [];
    private List<Point>? _basePoints; // snapshot for drag-move

    public IReadOnlyList<Point> Points => _points;

    public void AddPoint(Point p)
    {
        _points.Add(p);
        // Update bounding box
        if (_points.Count == 1)
        {
            Bounds = new Rect(p, new Size(1, 1));
        }
        else
        {
            double minX = Math.Min(Bounds.X, p.X);
            double minY = Math.Min(Bounds.Y, p.Y);
            double maxX = Math.Max(Bounds.Right, p.X);
            double maxY = Math.Max(Bounds.Bottom, p.Y);
            Bounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }

    public override void BeginMove()
    {
        _basePoints = new List<Point>(_points);
    }

    public override void MoveTo(double x, double y)
    {
        if (_basePoints == null) return;
        // delta from where points were at BeginMove time
        double origX = _basePoints.Min(p => p.X);
        double origY = _basePoints.Min(p => p.Y);
        double dx = x - origX;
        double dy = y - origY;
        Bounds = new Rect(x, y, Bounds.Width, Bounds.Height);
        _points.Clear();
        foreach (var p in _basePoints)
            _points.Add(new Point(p.X + dx, p.Y + dy));
    }

    public override void EndMove()
    {
        _basePoints = null;
    }

    public override void Draw(SKCanvas canvas, double scale)
    {
        if (_points.Count < 2) return;

        using var paint = CreateLinePaint(scale);
        paint.StrokeCap = SKStrokeCap.Round;
        paint.StrokeJoin = SKStrokeJoin.Round;

        var path = new SKPath();
        path.MoveTo((float)(_points[0].X * scale), (float)(_points[0].Y * scale));
        for (int i = 1; i < _points.Count; i++)
            path.LineTo((float)(_points[i].X * scale), (float)(_points[i].Y * scale));

        canvas.DrawPath(path, paint);
    }

    public override DrawableContainer Clone()
    {
        var clone = new FreehandContainer
        {
            Bounds = Bounds,
            Selected = Selected,
            LineColor = LineColor,
            FillColor = FillColor,
            LineThickness = LineThickness,
            Shadow = Shadow,
        };
        foreach (var p in _points)
            clone._points.Add(p);
        return clone;
    }
}
