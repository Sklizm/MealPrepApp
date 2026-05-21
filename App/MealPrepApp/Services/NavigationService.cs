using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private object? _currentView;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public object? CurrentView
    {
        get => _currentView;
        private set
        {
            _currentView = value;
            CurrentViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CurrentViewChanged;

    public Task NavigateToAsync<TViewModel>() where TViewModel : class =>
        NavigateToAsync(_services.GetRequiredService<TViewModel>());

    public async Task NavigateToAsync(object viewModel)
    {
        CurrentView = viewModel;
        if (viewModel is IAsyncLoadable loadable)
            await loadable.LoadAsync();
    }
}
