using System.Windows;
using MealPrepApp.ViewModels.Auth;
using MealPrepApp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Views.Auth;

/// <summary>
/// Hosts the Login and Register views and swaps between them. On a successful login it
/// opens the <see cref="ShellWindow"/> and closes itself.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _loginViewModel;
    private readonly RegisterViewModel _registerViewModel;

    public LoginWindow(LoginViewModel loginViewModel, RegisterViewModel registerViewModel)
    {
        InitializeComponent();

        _loginViewModel = loginViewModel;
        _registerViewModel = registerViewModel;

        _loginViewModel.SwitchToRegisterRequested += (_, _) => ShowRegister();
        _loginViewModel.LoginSucceeded += (_, _) => OpenShell();
        _registerViewModel.SwitchToLoginRequested += (_, _) => ShowLogin();
        _registerViewModel.RegistrationSucceeded += (_, _) => ShowLogin();

        ShowLogin();
    }

    private void ShowLogin() =>
        Host.Content = new LoginView { DataContext = _loginViewModel };

    private void ShowRegister() =>
        Host.Content = new RegisterView { DataContext = _registerViewModel };

    private async void OpenShell()
    {
        var loadingWindow = App.Services.GetRequiredService<StartupLoadingWindow>();
        Application.Current.MainWindow = loadingWindow;
        loadingWindow.Show();
        Hide();

        var shell = App.Services.GetRequiredService<ShellWindow>();
        var minimumDisplay = Task.Delay(TimeSpan.FromMilliseconds(3500));

        try
        {
            await Task.WhenAll(shell.InitializeBeforeShowAsync(), minimumDisplay);
            Application.Current.MainWindow = shell;
            shell.Show();
            loadingWindow.Close();
            Close();
        }
        catch
        {
            loadingWindow.Close();
            Show();
            Application.Current.MainWindow = this;
            throw;
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
