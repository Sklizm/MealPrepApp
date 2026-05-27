using System.Windows;
using MealPrepApp.ViewModels.Shell;
using MealPrepApp.Views.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Views;

public partial class ShellWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.SignOutRequested += OnSignOutRequested;
        _viewModel.ChangePasswordRequested += OnChangePasswordRequested;
        Closed += OnClosed;
    }

    /// <summary>
    /// Initializes the dashboard while the standalone startup loading window is visible.
    /// The shell is only shown after this task completes.
    /// </summary>
    public Task InitializeBeforeShowAsync() => _viewModel.InitializeAsync();

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.SignOutRequested -= OnSignOutRequested;
        _viewModel.ChangePasswordRequested -= OnChangePasswordRequested;
    }

    private void OnSignOutRequested(object? sender, EventArgs e)
    {
        var login = App.Services.GetRequiredService<LoginWindow>();
        Application.Current.MainWindow = login;
        login.Show();
        Close();
    }

    private void OnChangePasswordRequested(object? sender, EventArgs e)
    {
        var dialog = App.Services.GetRequiredService<ChangePasswordDialog>();
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "❐" : "▢";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
