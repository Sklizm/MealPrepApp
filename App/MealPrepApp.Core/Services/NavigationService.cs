using Microsoft.Extensions.DependencyInjection;
namespace MealPrepApp.Services;
public sealed class NavigationService : INavigationService, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<Type, object> _pages = new();
    private readonly SemaphoreSlim _gate=new(1,1);
    public NavigationService(IServiceProvider services) { _services=services; }
    public object? CurrentView { get; private set; }
    public event EventHandler? CurrentViewChanged;
    public void Dispose() { foreach(var page in _pages.Values.Append(CurrentView).Distinct()) if(page is IDisposable d) d.Dispose(); _pages.Clear(); CurrentView=null; }
    public Task NavigateToAsync<TViewModel>(Action<TViewModel>? prepare = null) where TViewModel:class
    {
        var type=typeof(TViewModel);
        // Parameterized editors/details are always constructed by the caller.
        if (!_pages.TryGetValue(type,out var page)) _pages[type]=page=_services.GetRequiredService<TViewModel>();
        return NavigateCoreAsync(page, prepare is null ? null : () => prepare((TViewModel)page));
    }
    public Task NavigateToAsync(object viewModel) => NavigateCoreAsync(viewModel, null);
    private async Task NavigateCoreAsync(object viewModel, Action? prepare)
    {
        await _gate.WaitAsync();
        try
        {
            if (ReferenceEquals(CurrentView,viewModel) && prepare is null) return;
            if (CurrentView is ILeaveGuard guard && !await guard.CanLeaveAsync()) return;
            prepare?.Invoke();
            var previous=CurrentView;
            CurrentView=viewModel; CurrentViewChanged?.Invoke(this,EventArgs.Empty);
            if (previous is IDisposable disposable && !_pages.Values.Contains(previous)) disposable.Dispose();
            if (viewModel is IAsyncLoadable loadable) await loadable.LoadAsync();
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) { AppDiagnostics.Record(ex); await _services.GetRequiredService<IDialogService>().ShowErrorAsync("Secțiunea nu a putut fi încărcată. Încearcă din nou."); }
        finally { _gate.Release(); }
    }
}
