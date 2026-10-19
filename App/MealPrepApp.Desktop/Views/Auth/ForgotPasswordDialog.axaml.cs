using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Auth;

namespace MealPrepApp.Desktop.Views.Auth;

public partial class ForgotPasswordDialog : Window
{
    public ForgotPasswordDialog()
    {
        InitializeComponent();
    }

    public void PrefillIdentifier(string identifier)
    {
        if (DataContext is ForgotPasswordViewModel vm && !string.IsNullOrWhiteSpace(identifier))
            vm.Identifier = identifier.Trim();
    }

    private async void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ForgotPasswordViewModel vm)
        {
            var p1 = NewPasswordBox.Text ?? "";
            var p2 = ConfirmPasswordBox.Text ?? "";
            var result = await vm.ResetAsync(p1, p2);
            if (result)
            {
                Tag = true;
                Close(true);
            }
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Tag = false;
        Close(false);
    }
}
