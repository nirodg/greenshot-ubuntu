using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Greenshot.Editor.Views;

public partial class ColorPickerDialog : Window
{
    private readonly Color[] _colors;
    private Color _selected;

    public ColorPickerDialog(Color[] colors, Color current)
    {
        _colors = colors;
        _selected = current;
        InitializeComponent();
        BuildColorGrid();
    }

    private void BuildColorGrid()
    {
        foreach (var color in _colors)
        {
            bool isCurrentColor = color == _selected;
            var border = new Border
            {
                Width = 30,
                Height = 30,
                Margin = new Thickness(2),
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(4),
                Cursor = new Cursor(StandardCursorType.Hand),
                BorderThickness = new Thickness(isCurrentColor ? 2 : 1),
                BorderBrush = isCurrentColor
                    ? new SolidColorBrush(Color.Parse("#4A90D9"))
                    : new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            };
            border.PointerEntered += (_, _) =>
                border.BorderBrush = new SolidColorBrush(Color.Parse("#4A90D9"));
            border.PointerExited += (_, _) =>
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            border.PointerPressed += (_, _) =>
            {
                _selected = color;
                Close(_selected);
            };
            ColorPanel.Children.Add(border);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
