using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
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
    [RelayCommand] private async Task OpenMeal(MealPlanEntry? entry) {
        if (entry is null || _services is null) return;
        var detail = _services.GetRequiredService<MealPrepApp.ViewModels.Retete.ReteteDetailViewModel>();
        detail.RecipeId = entry.RecipeID; detail.PlannedMealContext = entry;
        await _services.GetRequiredService<INavigationService>().NavigateToAsync(detail);
    }
    [RelayCommand] private async Task CookMeal(MealPlanEntry? entry) {
        if (entry is null || _services is null) return;
        if (entry.IsPrepared) { await ConsumeMeal(entry); return; }
        try {
            var recipe = await _services.GetRequiredService<RecipeRepository>().GetRecipeFullAsync(entry.RecipeID);
            if (recipe is null) return;
            var cooking = _services.GetRequiredService<CookingSession>();
            cooking.StartRecipe(recipe, entry.PlannedPortions > 0 ? entry.PlannedPortions : entry.Servings ?? recipe.Servings ?? 1);
            cooking.CookingBatchID = entry.CookingBatchID; cooking.Save();
            await _services.GetRequiredService<INavigationService>().NavigateToAsync(new MealPrepApp.ViewModels.Retete.CookingViewModel(cooking,_services));
        } catch (Exception ex) { AppDiagnostics.Record(ex); ErrorContent = ex is AppDbException db ? db.LocalizedMessage : L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog"); }
    }
    [RelayCommand] private async Task ConsumeMeal(MealPlanEntry? entry) {
        if (entry?.CookingBatchID is not int batch || entry.UnconsumedPortions <= 0 || _services?.GetService<CookingBatchRepository>() is not { } repository) return;
        try {
            if (!await _services.GetRequiredService<IDialogService>().ConfirmAsync(L.Message("UX.ConsumeLeftovers"),L.Message("UX.ConsumePrompt",entry.UnconsumedPortions,entry.RecipeTitle))) return;
            await repository.PortionEventAsync(_session.CurrentUserId,Guid.NewGuid(),batch,entry.BatchVersion,entry.UnconsumedPortions,"Consumed",entry.MealPlanEntryID);
            await LoadAsync();
        } catch (Exception ex) { AppDiagnostics.Record(ex); ErrorContent = ex is AppDbException db ? db.LocalizedMessage : L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog"); }
    }
    [RelayCommand] private Task ResumeCooking() => _services is null ? Task.CompletedTask :
        _services.GetRequiredService<INavigationService>().NavigateToAsync(new MealPrepApp.ViewModels.Retete.CookingViewModel(_services.GetRequiredService<CookingSession>(),_services));
    public bool CanResumeCooking => _services?.GetService<CookingSession>()?.Recipe is not null;
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
            OnPropertyChanged(nameof(CanResumeCooking));
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
