using System.Runtime.InteropServices;
using Avalonia;

namespace MealPrepApp.Desktop;

public static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            builder.With(new MacOSPlatformOptions
            {
                ShowInDock = true,
                DisableDefaultApplicationMenuItems = false
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            builder.With(new X11PlatformOptions
            {
                EnableMultiTouch = true,
                UseDBusMenu = true
            });
        }

        return builder;
    }
}
