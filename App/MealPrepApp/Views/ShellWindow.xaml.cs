using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
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
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await _viewModel.InitializeAsync();

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.SignOutRequested -= OnSignOutRequested;
        _viewModel.ChangePasswordRequested -= OnChangePasswordRequested;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsInitializing))
            AnimateLoadingOverlay(_viewModel.IsInitializing);
    }

    private void AnimateLoadingOverlay(bool show)
    {
        LoadingOverlay.BeginAnimation(OpacityProperty, null);

        var animation = new DoubleAnimation
        {
            To = show ? 1 : 0,
            Duration = TimeSpan.FromMilliseconds(show ? 160 : 220),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };

        if (show)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            animation.Completed += (_, _) =>
            {
                if (!_viewModel.IsInitializing)
                    LoadingOverlay.Visibility = Visibility.Collapsed;
            };
        }

        LoadingOverlay.BeginAnimation(OpacityProperty, animation);
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
