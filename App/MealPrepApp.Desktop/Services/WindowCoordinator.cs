using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MealPrepApp.Desktop.Views;
using MealPrepApp.Desktop.Views.Auth;
using MealPrepApp.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Desktop.Services;

internal static class WindowCoordinator
{
    public static void ShowLogin()
    {
        var app = (App)Application.Current!;
        var desktop = (IClassicDesktopStyleApplicationLifetime)app.ApplicationLifetime!;
        var previous = desktop.MainWindow;
        var oldScope = app.SessionScope;
        // Resolve a fresh session, including navigation and shell subscriptions.
        app.SessionScope = App.Services.CreateScope();
        var login = App.Services.GetRequiredService<LoginWindow>();
        desktop.MainWindow = login;
        login.Show();
        previous?.Close();
        oldScope?.Dispose();
    }

    public static void ShowShell()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        var previous = desktop.MainWindow;
        var shell = App.Services.GetRequiredService<ShellWindow>();
        shell.DataContext = App.Services.GetRequiredService<ShellViewModel>();
        desktop.MainWindow = shell;
        shell.Show();
        previous?.Close();
    }
}
