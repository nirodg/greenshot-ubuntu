using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;

namespace Greenshot.Views;

public partial class CaptureOverlayWindow : Window
{
    private Image<Rgba32>? _screenImage;
    private SKBitmap? _skBackground;

    private bool _isDragging;
    private Point _dragStart;
    private Point _dragCurrent;

    private readonly TaskCompletionSource<Rectangle?> _tcs = new();
    private readonly OverlayDrawOp _drawOp;

    public bool ShowWindowHighlight { get; set; } = false;

    public CaptureOverlayWindow(Image<Rgba32> screenImage)
    {
        _screenImage = screenImage;
        _skBackground = ConvertToSkBitmap(screenImage);
        _drawOp = new OverlayDrawOp(this);

        InitializeComponent();

        OverlayCanvas.IsHitTestVisible = true;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;
    }

    public Task<Rectangle?> WaitForSelectionAsync() => _tcs.Task;

    public override void Render(DrawingContext context)
    {
        context.Custom(_drawOp);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _dragStart = e.GetPosition(this);
            _dragCurrent = _dragStart;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            _dragCurrent = e.GetPosition(this);
            InvalidateVisual();
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;

        var rect = GetSelectionRect();
        Close();

        if (rect.Width < 3 || rect.Height < 3)
        {
            _tcs.TrySetResult(null);
            return;
        }

        _tcs.TrySetResult(new Rectangle(
            (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            _tcs.TrySetResult(null);
        }
    }

    private Rect GetSelectionRect()
    {
        double x = Math.Min(_dragStart.X, _dragCurrent.X);
        double y = Math.Min(_dragStart.Y, _dragCurrent.Y);
        double w = Math.Abs(_dragCurrent.X - _dragStart.X);
        double h = Math.Abs(_dragCurrent.Y - _dragStart.Y);
        return new Rect(x, y, w, h);
    }

    protected override void OnClosed(EventArgs e)
    {
        _tcs.TrySetResult(null); // ensure task always completes if window closes unexpectedly
        _skBackground?.Dispose();
        // _screenImage is owned by the caller, not disposed here
        base.OnClosed(e);
    }

    private static SKBitmap ConvertToSkBitmap(Image<Rgba32> img)
    {
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        ms.Position = 0;
        return SKBitmap.Decode(ms) ?? new SKBitmap(img.Width, img.Height);
    }

    private sealed class OverlayDrawOp : ICustomDrawOperation
    {
        private readonly CaptureOverlayWindow _win;
        public OverlayDrawOp(CaptureOverlayWindow win) => _win = win;
        public Rect Bounds => new(0, 0, _win.Bounds.Width, _win.Bounds.Height);
        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point p) => true;

        public void Render(ImmediateDrawingContext context)
        {
            var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
            if (lease == null) return;

            using (lease)
            {
                var canvas = lease.SkCanvas;
                canvas.Save();

                // Draw the captured screen
                if (_win._skBackground != null)
                {
                    using var imgPaint = new SKPaint { FilterQuality = SKFilterQuality.High };
                    canvas.DrawBitmap(_win._skBackground,
                        new SKRect(0, 0, (float)_win.Bounds.Width, (float)_win.Bounds.Height),
                        imgPaint);
                }

                // Dark overlay
                using var overlay = new SKPaint { Color = new SKColor(0, 0, 0, 120) };
                canvas.DrawRect(0, 0, (float)_win.Bounds.Width, (float)_win.Bounds.Height, overlay);

                if (_win._isDragging)
                {
                    var sel = _win.GetSelectionRect();
                    var skSel = new SKRect((float)sel.X, (float)sel.Y,
                        (float)sel.Right, (float)sel.Bottom);

                    // Clear dark overlay over selection (show original)
                    if (_win._skBackground != null)
                    {
                        canvas.Save();
                        canvas.ClipRect(skSel);
                        using var clearPaint = new SKPaint { BlendMode = SKBlendMode.Src };
                        canvas.DrawBitmap(_win._skBackground,
                            new SKRect(0, 0, (float)_win.Bounds.Width, (float)_win.Bounds.Height),
                            clearPaint);
                        canvas.Restore();
                    }

                    // Selection border
                    using var selBorder = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        Color = SKColors.White,
                        StrokeWidth = 1.5f
                    };
                    canvas.DrawRect(skSel, selBorder);

                    // Dimension text
                    var text = $"{(int)sel.Width} × {(int)sel.Height}";
                    using var textPaint = new SKPaint
                    {
                        Color = SKColors.White,
                        TextSize = 14,
                        IsAntialias = true
                    };
                    float tx = skSel.Left + 4;
                    float ty = skSel.Bottom + 18;
                    if (ty > (float)_win.Bounds.Height - 4) ty = skSel.Top - 4;
                    canvas.DrawText(text, tx, ty, textPaint);

                    // Zoomer in top-right corner
                    DrawZoomer(canvas, _win._skBackground, _win._dragCurrent);
                }
                else if (!_win._isDragging && _win._dragStart == _win._dragCurrent)
                {
                    // Show crosshair
                    DrawCrosshair(canvas, _win._dragCurrent);
                }

                canvas.Restore();
            }
        }

        private static void DrawZoomer(SKCanvas canvas, SKBitmap? bg, Point cursor)
        {
            if (bg == null) return;

            int zoomW = 120, zoomH = 80;
            int zoomFactor = 4;
            int srcW = zoomW / zoomFactor, srcH = zoomH / zoomFactor;
            int srcX = (int)cursor.X - srcW / 2;
            int srcY = (int)cursor.Y - srcH / 2;
            srcX = Math.Clamp(srcX, 0, bg.Width - srcW);
            srcY = Math.Clamp(srcY, 0, bg.Height - srcH);

            var src = new SKRect(srcX, srcY, srcX + srcW, srcY + srcH);
            var dst = new SKRect(bg.Width - zoomW - 10, 10, bg.Width - 10, 10 + zoomH);

            using var bgPaint = new SKPaint { FilterQuality = SKFilterQuality.None };
            canvas.DrawBitmap(bg, src, dst, bgPaint);

            using var border = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.White, StrokeWidth = 1 };
            canvas.DrawRect(dst, border);

            // Crosshair in zoomer
            float cx = dst.MidX, cy = dst.MidY;
            using var ch = new SKPaint { Color = SKColors.Red, StrokeWidth = 1 };
            canvas.DrawLine(cx - 8, cy, cx + 8, cy, ch);
            canvas.DrawLine(cx, cy - 8, cx, cy + 8, ch);
        }

        private static void DrawCrosshair(SKCanvas canvas, Point cursor)
        {
            using var paint = new SKPaint { Color = SKColors.White, StrokeWidth = 1 };
            // Horizontal line
            canvas.DrawLine(0, (float)cursor.Y, 9999, (float)cursor.Y, paint);
            // Vertical line
            canvas.DrawLine((float)cursor.X, 0, (float)cursor.X, 9999, paint);
        }

        public void Dispose() { }
    }
}
