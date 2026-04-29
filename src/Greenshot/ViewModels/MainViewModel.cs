using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Platform;
using Greenshot.Destinations;
using Greenshot.Editor.Views;
using Greenshot.Helpers;
using Greenshot.Views;
using Microsoft.Extensions.Logging;
using CoreConfiguration = Greenshot.Base.Core.CoreConfiguration;

namespace Greenshot.ViewModels;

public class MainViewModel
{
    private readonly CaptureHelper _captureHelper;
    private readonly ICoreConfiguration _config;
    private readonly IHotkeyProvider _hotkeyProvider;
    private readonly INotificationService _notifications;
    private readonly ILogger<MainViewModel> _logger;

    private IClassicDesktopStyleApplicationLifetime? _lifetime;

    public MainViewModel(
        CaptureHelper captureHelper,
        ICoreConfiguration config,
        IHotkeyProvider hotkeyProvider,
        INotificationService notifications,
        ILogger<MainViewModel> logger)
    {
        _captureHelper = captureHelper;
        _config = config;
        _hotkeyProvider = hotkeyProvider;
        _notifications = notifications;
        _logger = logger;
    }

    public void Initialize(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
        RegisterHotkeys();

        if (_config.IsFirstLaunch)
        {
            _config.IsFirstLaunch = false;
            if (_config is CoreConfiguration cfg) cfg.Save();
        }

        lifetime.Exit += OnAppExit;
        _logger.LogInformation("Greenshot started. Hotkeys registered.");
    }

    private void RegisterHotkeys()
    {
        if (!_hotkeyProvider.IsAvailable)
        {
            _logger.LogWarning("Hotkey provider not available (Wayland without XWayland?)");
            return;
        }

        TryRegisterHotkey("region", _config.RegionHotkey, CaptureRegionAsync);
        TryRegisterHotkey("fullscreen", _config.FullscreenHotkey, CaptureFullScreenAsync);
        TryRegisterHotkey("window", _config.WindowHotkey, CaptureWindowAsync);
        TryRegisterHotkey("lastregion", _config.LastRegionHotkey, CaptureLastRegionAsync);
    }

    private void TryRegisterHotkey(string id, string hotkey, Func<Task> action)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return;
        bool ok = _hotkeyProvider.RegisterHotkey(id, hotkey, () =>
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => action()));
        if (!ok)
            _logger.LogWarning("Failed to register hotkey {Id}: {Hotkey}", id, hotkey);
        else
            _logger.LogInformation("Hotkey {Id} = {Hotkey}", id, hotkey);
    }

    public NativeMenu BuildTrayMenu()
    {
        var menu = new NativeMenu();

        menu.Add(new NativeMenuItem("Capture Region")
        {
            Gesture = new Avalonia.Input.KeyGesture(Avalonia.Input.Key.None),
            Command = new RelayCommand(async () => await CaptureRegionAsync())
        });
        menu.Add(new NativeMenuItem("Capture Full Screen")
        {
            Command = new RelayCommand(async () => await CaptureFullScreenAsync())
        });
        menu.Add(new NativeMenuItem("Capture Window")
        {
            Command = new RelayCommand(async () => await CaptureWindowAsync())
        });
        menu.Add(new NativeMenuItem("Capture Last Region")
        {
            Command = new RelayCommand(async () => await CaptureLastRegionAsync())
        });
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(new NativeMenuItem("Settings...")
        {
            Command = new RelayCommand(OpenSettings)
        });
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(new NativeMenuItem("Quit Greenshot")
        {
            Command = new RelayCommand(Quit)
        });

        return menu;
    }

    public void OnTrayIconClicked()
    {
        // Default: show context menu (handled by tray icon itself)
    }

    private async Task CaptureRegionAsync()
    {
        var capture = await _captureHelper.CaptureRegionInteractiveAsync();
        if (capture != null) await HandleCaptureAsync(capture);
    }

    private async Task CaptureFullScreenAsync()
    {
        var capture = await _captureHelper.CaptureFullScreenAsync();
        if (capture != null) await HandleCaptureAsync(capture);
    }

    private async Task CaptureWindowAsync()
    {
        var capture = await _captureHelper.CaptureWindowInteractiveAsync();
        if (capture != null) await HandleCaptureAsync(capture);
    }

    private async Task CaptureLastRegionAsync()
    {
        var capture = await _captureHelper.CaptureLastRegionAsync();
        if (capture != null) await HandleCaptureAsync(capture);
    }

    private async Task HandleCaptureAsync(ICapture capture)
    {
        if (capture.Image == null)
        {
            _logger.LogError("HandleCaptureAsync: capture image is null");
            return;
        }

        _logger.LogInformation("HandleCaptureAsync: image {W}x{H}, destination={Dest}",
            capture.Image.Width, capture.Image.Height,
            _config.OutputDestinations.FirstOrDefault() ?? "(none)");

        var destinations = GetDestinations(capture);

        // Default: open in editor or save/picker
        var dest = _config.OutputDestinations.FirstOrDefault() ?? "Picker";

        if (dest == "FileDefault")
        {
            var fileDest = new FileDestination(_config);
            var result = await fileDest.ExportCaptureAsync(false, capture);
            if (result.Exported && result.FilePath != null && _config.ShowTrayNotification)
            {
                await _notifications.ShowNotificationAsync("Screenshot saved",
                    Path.GetFileName(result.FilePath));
            }
        }
        else
        {
            // "Editor" or "Picker": both open the editor window
            try
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var editor = EditorWindow.OpenWith(capture, destinations);
                    editor.Show();
                    editor.Activate();
                    _logger.LogInformation("Editor window opened");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open editor window");
            }
        }
    }

    private IEnumerable<IDestination> GetDestinations(ICapture capture)
    {
        return [
            new FileDestination(_config),
            new ClipboardDestination(),
        ];
    }

    private void OpenSettings()
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var win = new SettingsWindow(_config);
            win.Show();
        });
    }

    private void Quit()
    {
        _hotkeyProvider.UnregisterAll();
        _lifetime?.Shutdown();
    }

    private void OnAppExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _hotkeyProvider.UnregisterAll();
        if (_config is CoreConfiguration cfg) cfg.Save();
    }
}
