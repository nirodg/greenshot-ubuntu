using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Greenshot.Base.Core;
using SkiaSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Avalonia.Media.Color;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using Size = Avalonia.Size;

namespace Greenshot.Editor.Drawing;

public class Surface : Control, IDisposable
{
    private Image<Rgba32>? _backgroundImage;
    private SKBitmap? _skBackgroundBitmap;
    private readonly ObservableCollection<DrawableContainer> _elements = [];
    private DrawableContainer? _selectedElement;
    private DrawingMode _drawingMode = DrawingMode.None;
    private bool _isDrawing;
    private Point _drawStart;
    private DrawableContainer? _currentElement;

    // Drag/move state
    private bool _isDragging;
    private Point _dragStart;         // in image coordinates
    private Rect _dragElementStartBounds;

    // Undo/redo stacks
    private readonly Stack<List<DrawableContainer>> _undoStack = new();
    private readonly Stack<List<DrawableContainer>> _redoStack = new();

    // Color/thickness state
    public Color ActiveColor { get; set; } = Colors.Red;
    public Color FillColor { get; set; } = Colors.Transparent;
    public int LineThickness { get; set; } = 2;
    public DrawingMode DrawingMode
    {
        get => _drawingMode;
        set
        {
            _drawingMode = value;
            _selectedElement = null;
            InvalidateVisual();
        }
    }

    public IReadOnlyList<DrawableContainer> Elements => _elements;

    public event EventHandler? SurfaceChanged;
    public event EventHandler? SelectionChanged;
    public event EventHandler<TextContainer>? EditTextRequested;
    public event EventHandler<DrawableContainer>? DeleteRequested;

    public Surface()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public void SetBackgroundImage(Image<Rgba32>? image)
    {
        _skBackgroundBitmap?.Dispose();
        _backgroundImage = image;
        _skBackgroundBitmap = null;

        if (image != null)
        {
            _skBackgroundBitmap = ConvertToSkBitmap(image);
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        // Fill with transparent brush so Avalonia includes this control in pointer hit-testing
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var drawOp = new SurfaceDrawOperation(this);
        context.Custom(drawOp);
    }

    private double GetScale() =>
        _backgroundImage != null
            ? Math.Min(Bounds.Width / _backgroundImage.Width, Bounds.Height / _backgroundImage.Height)
            : 1.0;

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        _redoStack.Push(new List<DrawableContainer>(_elements));
        _elements.Clear();
        foreach (var e in _undoStack.Pop())
            _elements.Add(e);
        _selectedElement = null;
        InvalidateVisual();
        SurfaceChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        _undoStack.Push(new List<DrawableContainer>(_elements));
        _elements.Clear();
        foreach (var e in _redoStack.Pop())
            _elements.Add(e);
        _selectedElement = null;
        InvalidateVisual();
        SurfaceChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void DeleteSelectedElement()
    {
        if (_selectedElement == null) return;
        SaveUndoState();
        _elements.Remove(_selectedElement);
        _selectedElement = null;
        InvalidateVisual();
        SurfaceChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAllElements()
    {
        foreach (var e in _elements)
            e.Selected = true;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);

        // Right-click: select element and raise DeleteRequested
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            var scale = GetScale();
            var imagePos = new Point(pos.X / scale, pos.Y / scale);
            for (int i = _elements.Count - 1; i >= 0; i--)
            {
                if (_elements[i].HitTest(imagePos))
                {
                    if (_selectedElement != null) _selectedElement.Selected = false;
                    _selectedElement = _elements[i];
                    _selectedElement.Selected = true;
                    InvalidateVisual();
                    DeleteRequested?.Invoke(this, _selectedElement);
                    e.Handled = true;
                    return;
                }
            }
            e.Handled = true;
            return;
        }

        e.Pointer.Capture(this);
        Focus();
        _drawStart = pos;

        if (_drawingMode == DrawingMode.None)
        {
            HandleSelection(pos, e.ClickCount);
        }
        else
        {
            SaveUndoState();
            _currentElement = CreateElement(_drawingMode, pos);
            if (_currentElement != null)
            {
                _elements.Add(_currentElement);
                _isDrawing = true;
            }
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_isDragging && _selectedElement != null)
        {
            double scale = GetScale();
            var imagePos = new Point(pos.X / scale, pos.Y / scale);
            double dx = imagePos.X - _dragStart.X;
            double dy = imagePos.Y - _dragStart.Y;
            _selectedElement.MoveTo(_dragElementStartBounds.X + dx, _dragElementStartBounds.Y + dy);
            InvalidateVisual();
            return;
        }

        if (!_isDrawing || _currentElement == null) return;
        UpdateCurrentElement(pos);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured == this)
            e.Pointer.Capture(null);

        if (_isDragging)
        {
            _isDragging = false;
            _selectedElement?.EndMove();
            SurfaceChanged?.Invoke(this, EventArgs.Empty);
        }

        if (_isDrawing)
        {
            _isDrawing = false;
            if (_currentElement != null && IsDegenerate(_currentElement))
            {
                _elements.Remove(_currentElement);
                _currentElement.Dispose();
                if (_undoStack.Count > 0) _undoStack.Pop();
            }
            else if (_currentElement is ObfuscateContainer obf)
            {
                obf.SetCapturedRegion(ExtractBackgroundRegion(_currentElement.Bounds));
            }
            else if (_currentElement is TextContainer tc)
            {
                // Auto-open text editor immediately after placing a text element
                EditTextRequested?.Invoke(this, tc);
            }
            _currentElement = null;
            SurfaceChanged?.Invoke(this, EventArgs.Empty);
        }
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            DeleteSelectedElement();
            e.Handled = true;
        }
        else if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Redo();
            e.Handled = true;
        }
    }

    private void HandleSelection(Point pos, int clickCount)
    {
        var scale = GetScale();
        var imagePos = new Point(pos.X / scale, pos.Y / scale);

        DrawableContainer? hit = null;
        for (int i = _elements.Count - 1; i >= 0; i--)
        {
            if (_elements[i].HitTest(imagePos))
            {
                hit = _elements[i];
                break;
            }
        }

        if (_selectedElement != null && _selectedElement != hit)
            _selectedElement.Selected = false;

        _selectedElement = hit;

        if (_selectedElement != null)
        {
            _selectedElement.Selected = true;

            if (clickCount == 2 && hit is TextContainer tc)
            {
                EditTextRequested?.Invoke(this, tc);
            }
            else
            {
                // Begin drag-move
                _isDragging = true;
                _dragStart = imagePos;
                _dragElementStartBounds = hit!.Bounds;
                hit.BeginMove();
                SaveUndoState();
            }
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private DrawableContainer? CreateElement(DrawingMode mode, Point startPos)
    {
        double scale = GetScale();
        var imagePos = new Point(startPos.X / scale, startPos.Y / scale);
        var bounds = new Rect(imagePos, new Size(1, 1));

        DrawableContainer el = mode switch
        {
            DrawingMode.Rect => new RectangleContainer(),
            DrawingMode.Ellipse => new EllipseContainer(),
            DrawingMode.Arrow => new ArrowContainer(),
            DrawingMode.Text => new TextContainer { Text = "Text" },
            DrawingMode.Freehand => new FreehandContainer(),
            DrawingMode.Highlight => new HighlightContainer(),
            DrawingMode.Obfuscate => new ObfuscateContainer(),
            _ => new RectangleContainer()
        };

        el.Bounds = bounds;
        el.LineColor = ActiveColor;
        el.FillColor = FillColor;
        el.LineThickness = LineThickness;

        if (el is FreehandContainer fh) fh.AddPoint(imagePos);

        return el;
    }

    private void UpdateCurrentElement(Point pos)
    {
        if (_currentElement == null) return;
        double scale = GetScale();
        var imagePos = new Point(pos.X / scale, pos.Y / scale);
        var startImagePos = new Point(_drawStart.X / scale, _drawStart.Y / scale);

        if (_currentElement is FreehandContainer fh)
        {
            fh.AddPoint(imagePos);
            return;
        }

        double x = Math.Min(startImagePos.X, imagePos.X);
        double y = Math.Min(startImagePos.Y, imagePos.Y);
        double w = Math.Abs(imagePos.X - startImagePos.X);
        double h = Math.Abs(imagePos.Y - startImagePos.Y);

        if (_currentElement is ArrowContainer)
        {
            _currentElement.Bounds = new Rect(startImagePos.X, startImagePos.Y,
                imagePos.X - startImagePos.X, imagePos.Y - startImagePos.Y);
        }
        else
        {
            _currentElement.Bounds = new Rect(x, y, w, h);
        }
    }

    private static bool IsDegenerate(DrawableContainer el)
    {
        if (el is FreehandContainer fh) return fh.Points.Count < 3;
        // Arrows can have negative width/height (direction-dependent), use diagonal length
        if (el is ArrowContainer)
        {
            double len = Math.Sqrt(el.Bounds.Width * el.Bounds.Width + el.Bounds.Height * el.Bounds.Height);
            return len < 5;
        }
        return el.Bounds.Width < 3 && el.Bounds.Height < 3;
    }

    private SKBitmap? ExtractBackgroundRegion(Rect bounds)
    {
        if (_skBackgroundBitmap == null) return null;
        int x = Math.Max(0, (int)bounds.X);
        int y = Math.Max(0, (int)bounds.Y);
        int w = Math.Min((int)Math.Max(1, bounds.Width), _skBackgroundBitmap.Width - x);
        int h = Math.Min((int)Math.Max(1, bounds.Height), _skBackgroundBitmap.Height - y);
        if (w <= 0 || h <= 0) return null;
        var region = new SKBitmap(w, h);
        using var regionCanvas = new SKCanvas(region);
        regionCanvas.DrawBitmap(_skBackgroundBitmap,
            new SKRect(x, y, x + w, y + h),
            new SKRect(0, 0, w, h));
        return region;
    }

    private void SaveUndoState()
    {
        _undoStack.Push(_elements.Select(e => e.Clone()).ToList());
        _redoStack.Clear();
    }

    public Task<Image<Rgba32>?> RenderToImageAsync()
    {
        if (_backgroundImage == null) return Task.FromResult<Image<Rgba32>?>(null);

        using var skBitmap = ConvertToSkBitmap(_backgroundImage);
        using var canvas = new SKCanvas(skBitmap);

        foreach (var el in _elements)
            el.Draw(canvas, 1.0);

        canvas.Flush();
        var result = ConvertFromSkBitmap(skBitmap);
        return Task.FromResult<Image<Rgba32>?>(result);
    }

    private static SKBitmap ConvertToSkBitmap(Image<Rgba32> img)
    {
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        ms.Position = 0;
        return SKBitmap.Decode(ms) ?? new SKBitmap(img.Width, img.Height);
    }

    private static Image<Rgba32> ConvertFromSkBitmap(SKBitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Encode(ms, SKEncodedImageFormat.Png, 100);
        ms.Position = 0;
        return SixLabors.ImageSharp.Image.Load<Rgba32>(ms);
    }

    public void Dispose()
    {
        _skBackgroundBitmap?.Dispose();
        foreach (var el in _elements) el.Dispose();
        _elements.Clear();
        GC.SuppressFinalize(this);
    }

    private sealed class SurfaceDrawOperation : ICustomDrawOperation
    {
        private readonly Surface _surface;
        public SurfaceDrawOperation(Surface surface) => _surface = surface;
        public Rect Bounds => _surface.Bounds;
        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point p) => Bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            canvas.Save();
            canvas.Clear(SKColors.DarkGray);

            var scale = _surface.GetScale();
            if (_surface._skBackgroundBitmap != null)
            {
                // Center the image
                float drawW = (float)(_surface._skBackgroundBitmap.Width * scale);
                float drawH = (float)(_surface._skBackgroundBitmap.Height * scale);
                float offsetX = (float)((_surface.Bounds.Width - drawW) / 2);
                float offsetY = (float)((_surface.Bounds.Height - drawH) / 2);

                var dst = new SKRect(offsetX, offsetY, offsetX + drawW, offsetY + drawH);
                using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
                canvas.DrawBitmap(_surface._skBackgroundBitmap, dst, paint);

                // Draw annotations offset by image position
                canvas.Save();
                canvas.Translate(offsetX, offsetY);
                foreach (var el in _surface._elements)
                    el.Draw(canvas, scale);

                // Draw selection handles inside the same translate as elements
                if (_surface._selectedElement != null)
                {
                    var el = _surface._selectedElement;
                    // Normalize bounds (arrows can have negative W/H)
                    float left   = (float)(Math.Min(el.Bounds.X, el.Bounds.X + el.Bounds.Width)  * scale);
                    float top    = (float)(Math.Min(el.Bounds.Y, el.Bounds.Y + el.Bounds.Height) * scale);
                    float right  = (float)(Math.Max(el.Bounds.X, el.Bounds.X + el.Bounds.Width)  * scale);
                    float bottom = (float)(Math.Max(el.Bounds.Y, el.Bounds.Y + el.Bounds.Height) * scale);
                    var r = new SKRect(left, top, right, bottom);
                    using var selPaint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        Color = SKColors.DodgerBlue,
                        StrokeWidth = 1.5f,
                        PathEffect = SKPathEffect.CreateDash([5, 5], 0)
                    };
                    canvas.DrawRect(r, selPaint);
                }

                canvas.Restore();
            }

            canvas.Restore();
        }

        public void Dispose() { }
    }
}
