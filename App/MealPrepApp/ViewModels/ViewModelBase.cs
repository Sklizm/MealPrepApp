using CommunityToolkit.Mvvm.ComponentModel;

namespace MealPrepApp.ViewModels;

/// <summary>
/// Base for every ViewModel. Provides the shared async-state surface the design spec
/// calls for: <see cref="IsBusy"/> drives loading indicators and disables buttons,
/// <see cref="ErrorMessage"/> drives inline error slots.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>True while there is an error message to show.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    protected void ClearError() => ErrorMessage = null;
}
