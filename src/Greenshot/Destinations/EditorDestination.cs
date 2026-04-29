using Avalonia.Threading;
using Greenshot.Base.Core;
using Greenshot.Base.Interfaces;
using Greenshot.Editor.Views;

namespace Greenshot.Destinations;

public class EditorDestination : IDestination
{
    private readonly IEnumerable<IDestination> _destinations;

    public EditorDestination(IEnumerable<IDestination> destinations) => _destinations = destinations;

    public string Name => "Editor";
    public string Description => "Open in editor";
    public int Priority => 10;
    public bool IsActive => true;

    public async Task<ExportInformation> ExportCaptureAsync(
        bool manuallyInitiated, ICapture capture, CancellationToken ct = default)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var win = EditorWindow.OpenWith(capture, _destinations);
            win.Show();
        });
        return new ExportInformation(true);
    }
}
