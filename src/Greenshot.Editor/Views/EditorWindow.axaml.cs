using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.ViewModels;

namespace Greenshot.Editor.Views;

public partial class EditorWindow : Window
{
    private readonly EditorViewModel _vm = new();
    private double _zoom = 1.0;

    public EditorWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        EditorSurface.SurfaceChanged += OnSurfaceChanged;
        EditorSurface.EditTextRequested += OnEditTextRequested;
        EditorSurface.DeleteRequested += OnDeleteRequested;
        KeyDown += OnWindowKeyDown;
    }

    public static EditorWindow OpenWith(ICapture capture, IEnumerable<IDestination> destinations)
    {
        var win = new EditorWindow();
        win._vm.LoadCapture(capture);
        win._vm.Destinations = destinations;

        if (capture.Image != null)
        {
            win.EditorSurface.SetBackgroundImage(capture.Image);
            win.EditorSurface.Width = capture.Image.Width;
            win.EditorSurface.Height = capture.Image.Height;

            // Size the window to the image plus chrome (toolbar ~44px, menu ~30px, status ~26px, borders)
            const int ChromeExtra = 120;
            win.Width  = Math.Min(capture.Image.Width  + 20, 1600);
            win.Height = Math.Min(capture.Image.Height + ChromeExtra, 1000);
        }
        win.StatusLabel.Text = win._vm.StatusText;
        return win;
    }

    private void OnSurfaceChanged(object? sender, EventArgs e) => SurfaceChanged();

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) SetMode(DrawingMode.None);
        else if (e.Key == Key.R) SetMode(DrawingMode.Rect);
        else if (e.Key == Key.E) SetMode(DrawingMode.Ellipse);
        else if (e.Key == Key.A) SetMode(DrawingMode.Arrow);
        else if (e.Key == Key.T) SetMode(DrawingMode.Text);
        else if (e.Key == Key.F) SetMode(DrawingMode.Freehand);
        else if (e.Key == Key.H) SetMode(DrawingMode.Highlight);
        else if (e.Key == Key.B) SetMode(DrawingMode.Obfuscate);
    }

    private async void OnEditTextRequested(object? sender, Greenshot.Editor.Drawing.TextContainer tc)
    {
        var dialog = new TextEditDialog(tc.Text);
        var result = await dialog.ShowDialog<string?>(this);
        if (result != null)
        {
            tc.Text = result;
            EditorSurface.InvalidateVisual();
            SurfaceChanged();
        }
    }

    private void OnDeleteRequested(object? sender, Greenshot.Editor.Drawing.DrawableContainer el)
    {
        // Right-click on element = immediately delete it
        EditorSurface.DeleteSelectedElement();
    }

    private void SurfaceChanged() => StatusLabel.Text = _vm.StatusText;

    private void SetMode(DrawingMode mode)
    {
        _vm.DrawingMode = mode;
        EditorSurface.DrawingMode = mode;
        EditorSurface.ActiveColor = _vm.LineColor;
        EditorSurface.FillColor = _vm.FillColor;
        EditorSurface.LineThickness = _vm.LineThickness;
        StatusLabel.Text = mode == DrawingMode.None ? "Select / Move" : $"Tool: {mode}";

        // Highlight active tool button
        var toolButtons = new[]
        {
            (BtnSelect,    DrawingMode.None),
            (BtnRect,      DrawingMode.Rect),
            (BtnEllipse,   DrawingMode.Ellipse),
            (BtnArrow,     DrawingMode.Arrow),
            (BtnText,      DrawingMode.Text),
            (BtnFreehand,  DrawingMode.Freehand),
            (BtnHighlight, DrawingMode.Highlight),
            (BtnObfuscate, DrawingMode.Obfuscate),
        };
        foreach (var (btn, btnMode) in toolButtons)
        {
            if (btnMode == mode)
                btn.Classes.Add("active");
            else
                btn.Classes.Remove("active");
        }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        var config = CoreConfiguration.Load();
        var path = await _vm.SaveToFileAsync(EditorSurface,
            config.OutputFilePath, config.OutputFileFilenamePattern,
            config.OutputFileFormat, config.OutputFileJpegQuality);

        if (path != null)
        {
            StatusLabel.Text = $"Saved: {path}";
            if (config.OutputFileCopyPathToClipboard)
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                    await clipboard.SetTextAsync(path);
            }
        }
    }

    private async void OnSaveAs(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Screenshot",
            DefaultExtension = "png",
            FileTypeChoices = [
                new FilePickerFileType("PNG Image") { Patterns = ["*.png"] },
                new FilePickerFileType("JPEG Image") { Patterns = ["*.jpg", "*.jpeg"] },
                new FilePickerFileType("BMP Image") { Patterns = ["*.bmp"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] }
            ]
        });

        if (file == null) return;

        var path = file.Path.LocalPath;
        var ext = Path.GetExtension(path).TrimStart('.').ToLower();
        var format = ext switch
        {
            "jpg" or "jpeg" => OutputFormat.Jpg,
            "bmp" => OutputFormat.Bmp,
            "tiff" or "tif" => OutputFormat.Tiff,
            _ => OutputFormat.Png
        };

        var rendered = await EditorSurface.RenderToImageAsync();
        if (rendered == null) return;

        await ImageSaveHelper.SaveAsync(rendered, path, format);
        StatusLabel.Text = $"Saved: {path}";
    }

    private async void OnCopyToClipboard(object? sender, RoutedEventArgs e)
    {
        var rendered = await EditorSurface.RenderToImageAsync();
        if (rendered == null) return;

        var bytes = await ImageSaveHelper.ToBytesAsync(rendered, OutputFormat.Png);
        var dataObj = new DataObject();
        dataObj.Set("image/png", bytes);
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
            await clipboard.SetDataObjectAsync(dataObj);
        StatusLabel.Text = "Copied to clipboard";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnUndo(object? sender, RoutedEventArgs e) => EditorSurface.Undo();
    private void OnRedo(object? sender, RoutedEventArgs e) => EditorSurface.Redo();
    private void OnSelectAll(object? sender, RoutedEventArgs e) => EditorSurface.SelectAllElements();
    private void OnDeleteSelected(object? sender, RoutedEventArgs e) => EditorSurface.DeleteSelectedElement();

    private void OnModeSelect(object? sender, RoutedEventArgs e) => SetMode(DrawingMode.None);
    private void OnModeRect(object? sender, RoutedEventArgs e) => SetMode(DrawingMode.Rect);
    private void OnModeEllipse(object? sender, RoutedEventArgs e) => SetMode(DrawingMode.Ellipse);
    private void OnModeArrow(object? sender, RoutedEventArgs e) => SetMode(DrawingMode.Arrow);
    private void OnModeText(object? sender, RoutedEventArgs e) => SetMode(DrawingMode.Text);
    private void OnModeFreehand(object? sender, RoutedEventArgs e) => SetMode(DrawingMode.Freehand);
    private void OnModeHighlight(object? sender, RoutedEventArgs e) => SetMode(DrawingMode.Highlight);
    private void OnModeObfuscate(object? sender, RoutedEventArgs e) => SetMode(DrawingMode.Obfuscate);

    private async void OnColorClick(object? sender, PointerPressedEventArgs e)
    {
        var colors = new[]
        {
            // Row 1 — reds / warm
            Colors.Red,       Color.Parse("#FF4444"), Color.Parse("#FF6B35"),
            Colors.OrangeRed, Colors.Orange,          Colors.Yellow,
            // Row 2 — greens / cyans
            Color.Parse("#AAFF00"), Colors.LimeGreen, Colors.Green,
            Colors.Teal,            Colors.Cyan,      Color.Parse("#00BFFF"),
            // Row 3 — blues / purples
            Colors.DodgerBlue, Colors.Blue,       Color.Parse("#5B2D8E"),
            Colors.Purple,     Colors.Magenta,    Color.Parse("#FF69B4"),
            // Row 4 — neutrals
            Colors.White,      Color.Parse("#D0D0D0"), Color.Parse("#A0A0A0"),
            Color.Parse("#606060"), Color.Parse("#303030"), Colors.Black,
        };

        var dialog = new ColorPickerDialog(colors, _vm.LineColor);
        var result = await dialog.ShowDialog<Color?>(this);
        if (result.HasValue)
        {
            _vm.LineColor = result.Value;
            ColorPreview.Background = new SolidColorBrush(result.Value);
            EditorSurface.ActiveColor = result.Value;
        }
    }

    private void OnThicknessChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (EditorSurface == null) return;
        if (ThicknessCombo.SelectedItem is ComboBoxItem item)
        {
            // Content is e.g. "3 px" — parse the leading number
            var raw = item.Content?.ToString()?.Split(' ')[0] ?? "2";
            if (int.TryParse(raw, out int thickness))
            {
                _vm.LineThickness = thickness;
                EditorSurface.LineThickness = thickness;
            }
        }
    }

    private void OnZoomIn(object? sender, RoutedEventArgs e)
    {
        _zoom = Math.Min(_zoom * 1.25, 8.0);
        ApplyZoom();
    }

    private void OnZoomOut(object? sender, RoutedEventArgs e)
    {
        _zoom = Math.Max(_zoom / 1.25, 0.1);
        ApplyZoom();
    }

    private void OnZoomFit(object? sender, RoutedEventArgs e)
    {
        _zoom = 1.0;
        // Clear explicit dimensions so Surface fills the window again (true "fit to window")
        EditorSurface.Width = double.NaN;
        EditorSurface.Height = double.NaN;
    }

    private void ApplyZoom()
    {
        if (_vm.Capture?.Image != null)
        {
            EditorSurface.Width = _vm.Capture.Image.Width * _zoom;
            EditorSurface.Height = _vm.Capture.Image.Height * _zoom;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        EditorSurface.Dispose();
        base.OnClosing(e);
    }
}
