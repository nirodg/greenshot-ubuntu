using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Greenshot.Editor.Views;

public partial class TextEditDialog : Window
{
    public TextEditDialog(string currentText)
    {
        InitializeComponent();
        InputBox.Text = currentText;
        Opened += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(InputBox.Text ?? string.Empty);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
