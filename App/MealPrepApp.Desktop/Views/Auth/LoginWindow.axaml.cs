using Avalonia.Controls;
using MealPrepApp.Desktop.Views.Shared;
using MealPrepApp.ViewModels.Auth;
using MealPrepApp.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Desktop.Views.Auth;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _loginViewModel;
    private readonly RegisterViewModel _registerViewModel;

    public LoginWindow() : this(
        App.Services.GetRequiredService<LoginViewModel>(),
        App.Services.GetRequiredService<RegisterViewModel>())
    {
    }

    public LoginWindow(LoginViewModel loginViewModel, RegisterViewModel registerViewModel)
    {
        InitializeComponent();

        _loginViewModel = loginViewModel;
        _registerViewModel = registerViewModel;

        _loginViewModel.SwitchToRegisterRequested += (_, _) => ShowRegister();
        _loginViewModel.ForgotPasswordRequested += (_, _) => ShowForgotPasswordDialog();
        _loginViewModel.LoginSucceeded += (_, _) => OpenShell();
        _registerViewModel.SwitchToLoginRequested += (_, _) => ShowLogin();
        _registerViewModel.RegistrationSucceeded += (_, _) => ShowLogin();

        ShowLogin();
    }

    private void ShowLogin() =>
        Host.Content = new LoginView { DataContext = _loginViewModel };

    private void ShowRegister() =>
        Host.Content = new RegisterView { DataContext = _registerViewModel };

    private void ShowForgotPasswordDialog() =>
        _ = MealPrepApp.Desktop.Services.UiOperation.RunAsync(() => App.Services.GetRequiredService<MealPrepApp.Services.IDialogService>()
            .ShowInfoAsync("Recuperare cont", "Contacteaza administratorul pentru recuperarea contului."));

    private async void EditConnection(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var app = (App)Avalonia.Application.Current!;
        string? savedConnection = null;
        ConnectionSetupWindow setup = null!;
        setup = new ConnectionSetupWindow(app.ConnectionSettings, connection =>
        {
            savedConnection = connection;
            setup.Close();
            return Task.CompletedTask;
        });
        await setup.ShowDialog(this);
        if (savedConnection != null) app.ApplyConnection(savedConnection);
    }

    private void OpenShell() => MealPrepApp.Desktop.Services.WindowCoordinator.ShowShell();
}
