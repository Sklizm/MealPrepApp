using System.Windows;
using MealPrepApp.ViewModels.Auth;

namespace MealPrepApp.Views.Auth;

public partial class ForgotPasswordDialog : Window
{
    private readonly ForgotPasswordViewModel _viewModel;

    public ForgotPasswordDialog(ForgotPasswordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    public void PrefillIdentifier(string identifier)
    {
        if (!string.IsNullOrWhiteSpace(identifier))
            _viewModel.Identifier = identifier.Trim();
    }

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.ResetAsync(NewPasswordBox.Password, ConfirmPasswordBox.Password))
        {
            DialogResult = true;
            Close();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnChromeCloseClick(object sender, RoutedEventArgs e) => Close();
}
