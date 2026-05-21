using System.Windows;
using System.Windows.Controls;
using MealPrepApp.ViewModels.Auth;

namespace MealPrepApp.Views.Auth;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
            await viewModel.LoginAsync(PasswordBox.Password);
    }
}
