using Avalonia.Media;
using SkiaSharp;

namespace Greenshot.Editor.Drawing;

public class TextContainer : DrawableContainer
{
    public string Text { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Sans";
    public float FontSize { get; set; } = 12f;
    public bool Bold { get; set; } = false;
    public bool Italic { get; set; } = false;
    public Color TextColor { get; set; } = Colors.Red;

    public override void Draw(SKCanvas canvas, double scale)
    {
        if (string.IsNullOrEmpty(Text)) return;

        var rect = GetSkRect(scale);

        if (FillColor != Colors.Transparent)
        {
            using var bg = CreateFillPaint();
            canvas.DrawRect(rect, bg);
        }

        using var textPaint = new SKPaint
        {
            Color = new SKColor(TextColor.R, TextColor.G, TextColor.B, TextColor.A),
            TextSize = FontSize * (float)scale,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(FontFamily,
                Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright)
        };

        float y = rect.Top + textPaint.TextSize;
        foreach (var line in Text.Split('\n'))
        {
            canvas.DrawText(line, rect.Left + 2, y, textPaint);
            y += textPaint.TextSize * 1.2f;
            if (y > rect.Bottom) break;
        }

        if (Selected)
        {
            using var selPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColors.Blue,
                StrokeWidth = 1,
                PathEffect = SKPathEffect.CreateDash([4, 4], 0)
            };
            canvas.DrawRect(rect, selPaint);
        }
    }
}
