using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels;

/// <summary>Backs the Acasa dashboard: KPI tiles + the "Retete Recente" grid.</summary>
public sealed partial class AcasaViewModel : ViewModelBase, IAsyncLoadable
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

    public async Task LoadAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            if(_mealPlan != null) { var today=await _mealPlan.GetWeeklyPlanAsync(_session.CurrentUserId,DateTime.Today); TodayMeals.Clear(); foreach(var entry in today.Where(e=>e.PlannedDate.Date==DateTime.Today)) TodayMeals.Add(entry); }
            if(_drafts != null) HasDrafts=(await _drafts.GetDraftsAsync(_session.CurrentUserId)).Count>0;
            Counts = await _dashboard.GetDashboardCountsAsync(_session.CurrentUserId);

            var recent = await _dashboard.GetRecentRecipesAsync(_session.CurrentUserId);
            RecentRecipes.Clear();
            foreach (var recipe in recent)
                RecentRecipes.Add(recipe);
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            ErrorMessage = "Operatia nu a putut fi finalizata. Consulta jurnalul aplicatiei.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Clicking a KPI tile jumps to that section (e.g. "Retete", "Planificare").</summary>
    [RelayCommand]
    private Task OpenSection(string section) => _shell.ShowSectionAsync(section);
}
