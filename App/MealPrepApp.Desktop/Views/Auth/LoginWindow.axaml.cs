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

    private async void ShowForgotPasswordDialog()
    {
        var dialog = new ForgotPasswordDialog
        {
            DataContext = App.Services.GetRequiredService<ForgotPasswordViewModel>()
        };
        dialog.PrefillIdentifier(_loginViewModel.Identifier);

        var result = await dialog.ShowDialog<bool>(this);
        if (result)
        {
            MessageDialog.ShowModal(
                MessageDialogKind.Info,
                "Parola resetata",
                "Parola a fost resetata. Te poti conecta cu parola noua.");
        }
    }

    private void OpenShell()
    {
        var shell = App.Services.GetRequiredService<ShellWindow>();
        shell.DataContext = App.Services.GetRequiredService<ShellViewModel>();
        shell.Show();
        Close();
    }
}
