using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Auth;

namespace MealPrepApp.Desktop.Views.Auth;

public partial class ChangePasswordDialog : Window
{
    public ChangePasswordDialog()
    {
        InitializeComponent();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm)
        {
            var pCurrent = CurrentPasswordBox.Text ?? "";
            var pNew = NewPasswordBox.Text ?? "";
            var pConfirm = ConfirmPasswordBox.Text ?? "";

            var changed = await vm.ChangeAsync(pCurrent, pNew, pConfirm);
            if (changed)
            {
                Tag = true;
                Close();
            }
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Tag = false;
        Close();
    }
}
