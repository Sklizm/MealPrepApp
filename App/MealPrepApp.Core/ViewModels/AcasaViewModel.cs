using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels;

/// <summary>Backs the Home dashboard: KPI tiles + the "Recent recipes" grid.</summary>
public sealed partial class AcasaViewModel : ViewModelBase, IAsyncLoadable, IDisposable
{
    private readonly DashboardRepository _dashboard;
    private readonly ISessionService _session;
    private readonly IShellNavigator _shell;

    [ObservableProperty] private string _searchTerm="";
    public ObservableCollection<MealPlanEntry> TodayMeals { get; }=new();
    [ObservableProperty] private bool _hasDrafts;
    private readonly MealPlanRepository? _mealPlan;
    private readonly DraftRepository? _drafts;
    [RelayCommand] private Task Search() => _shell.ShowSectionAsync("Retete:Cauta:"+SearchTerm);
    [RelayCommand] private Task OpenRecipe(RecentRecipe? recipe) => recipe is null ? Task.CompletedTask : OpenRecentAsync(recipe);
    private readonly IServiceProvider? _services;
    private async Task OpenRecentAsync(RecentRecipe recipe)
    {
        if(_services is null) return;
        var detail=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MealPrepApp.ViewModels.Retete.ReteteDetailViewModel>(_services);
        detail.RecipeId=recipe.RecipeID;
        await Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<INavigationService>(_services).NavigateToAsync(detail);
    }
    [RelayCommand] private Task Retry() => LoadAsync();
    [ObservableProperty]
    private DashboardCounts? _counts;

    public ObservableCollection<RecentRecipe> RecentRecipes { get; } = new();

    public AcasaViewModel(DashboardRepository dashboard, ISessionService session, IShellNavigator shell, MealPlanRepository? mealPlan=null, DraftRepository? drafts=null, IServiceProvider? services=null)
    {
        _dashboard = dashboard;
        _session = session;
        _shell = shell; _mealPlan=mealPlan; _drafts=drafts; _services=services;
    }

    private int _loadVersion;
    private bool _disposed;
    private CancellationTokenSource? _loadCts;
    public override void Dispose() { ReleaseLocalization(); if (_disposed) return; _disposed = true; ++_loadVersion; _loadCts?.Cancel(); _loadCts?.Dispose(); }

    public async Task LoadAsync()
    {
        if (_disposed) return;
        var version = ++_loadVersion;
        _loadCts?.Cancel(); _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource(); var token = _loadCts.Token;
        var userId = _session.CurrentUserId; var date = DateTime.Today;
        ClearError(); IsBusy = true;
        try
        {
            // Exactly four independent reads, with one connection per stored procedure.
            var mealsTask = _mealPlan?.GetWeeklyPlanAsync(userId, date, token) ?? Task.FromResult<IReadOnlyList<MealPlanEntry>>(Array.Empty<MealPlanEntry>());
            var draftsTask = _drafts?.GetDraftsAsync(userId, token) ?? Task.FromResult<IReadOnlyList<RecipeDraftListItem>>(Array.Empty<RecipeDraftListItem>());
            var countsTask = _dashboard.GetDashboardCountsAsync(userId, token);
            var recentTask = _dashboard.GetRecentRecipesAsync(userId, cancellationToken: token);
            await Task.WhenAll(mealsTask, draftsTask, countsTask, recentTask);
            if (version != _loadVersion || _disposed) return;
            TodayMeals.Clear(); foreach (var entry in mealsTask.Result.Where(e => e.PlannedDate.Date == date)) TodayMeals.Add(entry);
            HasDrafts = draftsTask.Result.Count > 0; Counts = countsTask.Result;
            RecentRecipes.Clear(); foreach (var recipe in recentTask.Result) RecentRecipes.Add(recipe);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (version == _loadVersion && !_disposed)
            {
                AppDiagnostics.Record(ex);
                ErrorContent = ex is AppDbException db ? db.LocalizedMessage : L.Message("Text.ThisSectionCouldNotBeLoadedTryAgain");
            }
        }
        finally { if (version == _loadVersion && !_disposed) IsBusy = false; }
    }

    /// <summary>Clicking a KPI tile jumps to that section (e.g. "Retete", "Planificare").</summary>
    [RelayCommand]
    private Task OpenSection(string section) => _shell.ShowSectionAsync(section);
}
