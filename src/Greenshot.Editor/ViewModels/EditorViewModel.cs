using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces;
using Greenshot.Editor.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using IImage = SixLabors.ImageSharp.Image;

namespace Greenshot.Editor.ViewModels;

public class EditorViewModel : INotifyPropertyChanged
{
    private DrawingMode _drawingMode = DrawingMode.None;
    private Color _lineColor = Colors.Red;
    private Color _fillColor = Colors.Transparent;
    private int _lineThickness = 2;
    private string _statusText = "Ready";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICapture? Capture { get; private set; }
    public IEnumerable<IDestination> Destinations { get; set; } = [];

    public DrawingMode DrawingMode
    {
        get => _drawingMode;
        set { _drawingMode = value; OnPropertyChanged(); }
    }

    public Color LineColor
    {
        get => _lineColor;
        set { _lineColor = value; OnPropertyChanged(); }
    }

    public Color FillColor
    {
        get => _fillColor;
        set { _fillColor = value; OnPropertyChanged(); }
    }

    public int LineThickness
    {
        get => _lineThickness;
        set { _lineThickness = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public void LoadCapture(ICapture capture)
    {
        Capture = capture;
        StatusText = $"Capture: {capture.CaptureDetails.DateTime:HH:mm:ss} - {capture.CaptureDetails.Title}";
    }

    public async Task<string?> SaveToFileAsync(Surface surface, string directory, string pattern, OutputFormat format, int jpegQuality)
    {
        var rendered = await surface.RenderToImageAsync();
        if (rendered == null) return null;

        var details = Capture?.CaptureDetails ?? new CaptureDetails();
        var baseName = FilenameHelper.FillPattern(pattern, details);
        var ext = ImageSaveHelper.GetExtension(format);
        var path = FilenameHelper.GetUniqueFilename(directory, baseName, ext);

        await ImageSaveHelper.SaveAsync(rendered, path, format, jpegQuality);
        return path;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
