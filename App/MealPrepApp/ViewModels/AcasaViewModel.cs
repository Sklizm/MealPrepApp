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

    [ObservableProperty]
    private DashboardCounts? _counts;

    public ObservableCollection<RecentRecipe> RecentRecipes { get; } = new();

    public AcasaViewModel(DashboardRepository dashboard, ISessionService session, IShellNavigator shell)
    {
        _dashboard = dashboard;
        _session = session;
        _shell = shell;
    }

    public async Task LoadAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
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
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Clicking a KPI tile jumps to that section (e.g. "Retete", "Planificare").</summary>
    [RelayCommand]
    private Task OpenSection(string section) => _shell.ShowSectionAsync(section);
}
