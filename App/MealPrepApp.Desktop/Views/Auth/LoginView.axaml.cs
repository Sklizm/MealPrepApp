using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Auth;

namespace MealPrepApp.Desktop.Views.Auth;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void OnLoginClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            var password = PasswordBox.Text ?? "";
            _ = vm.LoginAsync(password);
        }
    }
}
