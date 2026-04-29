using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Greenshot.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Greenshot;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainViewModel>();
            mainVm.Initialize(desktop);

            SetupTrayIcon(mainVm);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(MainViewModel mainVm)
    {
        _trayIcon = new TrayIcon
        {
            ToolTipText = "Greenshot - Screenshot Tool",
            Menu = mainVm.BuildTrayMenu()
        };

        // Load icon from assets
        try
        {
            var iconStream = typeof(App).Assembly
                .GetManifestResourceStream("Greenshot.Assets.greenshot.ico");
            if (iconStream != null)
                _trayIcon.Icon = new WindowIcon(iconStream);
        }
        catch { /* icon not critical */ }

        _trayIcon.Clicked += (_, _) => mainVm.OnTrayIconClicked();

        var icons = new TrayIcons { _trayIcon };
        TrayIcon.SetIcons(this, icons);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Platform services (Linux X11)
        services.AddSingleton<Greenshot.Base.Platform.IScreenCaptureProvider,
            Greenshot.Linux.X11.X11ScreenCapture>();
        services.AddSingleton<Greenshot.Base.Platform.IHotkeyProvider,
            Greenshot.Linux.X11.X11HotkeyProvider>();
        services.AddSingleton<Greenshot.Base.Platform.INotificationService,
            Greenshot.Linux.DBus.DbusNotificationService>();

        // Core configuration (registered as both concrete and interface)
        services.AddSingleton<Greenshot.Base.Core.CoreConfiguration>(_ =>
            Greenshot.Base.Core.CoreConfiguration.Load());
        services.AddSingleton<Greenshot.Base.Core.ICoreConfiguration>(sp =>
            sp.GetRequiredService<Greenshot.Base.Core.CoreConfiguration>());

        // Main view model and helpers
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<Helpers.CaptureHelper>();
    }
}
