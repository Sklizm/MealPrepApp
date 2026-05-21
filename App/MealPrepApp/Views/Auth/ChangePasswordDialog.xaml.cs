using System.Windows;
using MealPrepApp.ViewModels.Auth;

namespace MealPrepApp.Views.Auth;

public partial class ChangePasswordDialog : Window
{
    private readonly ChangePasswordViewModel _viewModel;

    public ChangePasswordDialog(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var changed = await _viewModel.ChangeAsync(
            CurrentPasswordBox.Password,
            NewPasswordBox.Password,
            ConfirmPasswordBox.Password);

        if (changed)
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

    private void OnChromeCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
