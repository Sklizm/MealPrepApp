using System.Windows;
using System.Windows.Controls;
using MealPrepApp.ViewModels.Auth;

namespace MealPrepApp.Views.Auth;

public partial class RegisterView : UserControl
{
    public RegisterView()
    {
        InitializeComponent();
    }

    private async void OnRegisterClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is RegisterViewModel viewModel)
            await viewModel.RegisterAsync(PasswordBox.Password, ConfirmPasswordBox.Password);
    }
}
