using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Auth;

namespace MealPrepApp.Desktop.Views.Auth;

public partial class RegisterView : UserControl
{
    public RegisterView()
    {
        InitializeComponent();
    }

    private void OnRegisterClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RegisterViewModel vm)
        {
            var p1 = PasswordBox.Text ?? "";
            var p2 = ConfirmPasswordBox.Text ?? "";
            _ = vm.RegisterAsync(p1, p2);
        }
    }
}
