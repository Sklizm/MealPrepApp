using CommunityToolkit.Mvvm.ComponentModel;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels;

/// <summary>Shared asynchronous state and messages whose identities survive language changes.</summary>
public abstract partial class ViewModelBase : LocalizedObservableObject
{
    [ObservableProperty] private bool _isBusy;
    private LocalizedMessage? _errorContent;
    private string? _literalError;
    public LocalizedMessage? ErrorContent
    {
        get => _errorContent;
        set { _literalError = null; _errorContent = value; OnPropertyChanged(nameof(ErrorMessage)); OnPropertyChanged(nameof(HasError)); }
    }
    [LocalizedProperty]
    public string? ErrorMessage
    {
        get => _errorContent?.ToString() ?? _literalError;
        set { _errorContent = null; _literalError = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }
    private LocalizedMessage? _notificationContent;
    private string? _literalNotification;
    public LocalizedMessage? NotificationContent
    {
        get => _notificationContent;
        set { _literalNotification = null; _notificationContent = value; OnPropertyChanged(nameof(Notification)); }
    }
    [LocalizedProperty]
    public string? Notification
    {
        get => _notificationContent?.ToString() ?? _literalNotification;
        set { _notificationContent = null; _literalNotification = value; OnPropertyChanged(); }
    }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    protected void ClearError() => ErrorContent = null;
}
