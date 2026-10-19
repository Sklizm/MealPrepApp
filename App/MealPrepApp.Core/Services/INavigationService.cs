namespace MealPrepApp.Services;

/// <summary>Swaps the ViewModel shown in the shell's content region. The shell's
/// ContentControl binds to <see cref="CurrentView"/>; DataTemplates pick the matching View.</summary>
public interface INavigationService
{
    object? CurrentView { get; }

    event EventHandler? CurrentViewChanged;

    /// <summary>Resolves <typeparamref name="TViewModel"/> from DI, shows it, and awaits its
    /// <see cref="IAsyncLoadable.LoadAsync"/> if it implements it.</summary>
    Task NavigateToAsync<TViewModel>() where TViewModel : class;

    /// <summary>Shows an already-constructed ViewModel (used when it needs parameters).</summary>
    Task NavigateToAsync(object viewModel);
}
