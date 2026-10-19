using Avalonia.Controls;
using MealPrepApp.Desktop.Views.Auth;
using MealPrepApp.ViewModels.Auth;
using MealPrepApp.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Desktop.Views;

public partial class ShellWindow : Window
{
    private ShellViewModel? _viewModel;

    public ShellWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is ShellViewModel vm)
            {
                _viewModel = vm;
                vm.SignOutRequested += OnSignOutRequested;
                vm.ChangePasswordRequested += OnChangePasswordRequested;
                await vm.InitializeAsync();
            }
        };
    }

    private void OnSignOutRequested(object? sender, EventArgs e)
    {
        var loginWindow = App.Services.GetRequiredService<LoginWindow>();
        loginWindow.Show();
        Close();
    }

    private void OnChangePasswordRequested(object? sender, EventArgs e)
    {
        var dialog = new ChangePasswordDialog
        {
            DataContext = App.Services.GetRequiredService<ChangePasswordViewModel>()
        };
        dialog.ShowDialog(this);
    }
}
