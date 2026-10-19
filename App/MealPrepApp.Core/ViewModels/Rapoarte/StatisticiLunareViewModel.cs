using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels.Rapoarte;

/// <summary>
/// "Statistici lunare" sub-tab of Rapoarte. Pick a month, hit Afiseaza, and see headline
/// counts (total meals, distinct recipes/ingredients), a per-slot breakdown, the top 5
/// most-planned recipes and the top 10 most-used ingredients. Backed by
/// <c>sp_GetMonthlyStats</c> / <c>sp_GetTopRecipes</c> / <c>sp_GetTopIngredients</c> via
/// <see cref="ReportRepository"/>.
/// </summary>
public sealed partial class StatisticiLunareViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly ReportRepository _reports;
    private readonly ISessionService _session;

    public ObservableCollection<int> Years { get; } = new();
    public ObservableCollection<MonthOption> Months { get; } = new();
    public ObservableCollection<SlotCount> SlotCounts { get; } = new();
    public ObservableCollection<TopRecipe> TopRecipes { get; } = new();
    public ObservableCollection<TopIngredient> TopIngredients { get; } = new();

    [ObservableProperty]
    private int _selectedYear;

    [ObservableProperty]
    private MonthOption? _selectedMonth;

    [ObservableProperty]
    private MonthlyStats? _stats;

    /// <summary>True once a month has data to display — drives the content panel.</summary>
    [ObservableProperty]
    private bool _hasData;

    /// <summary>True after a load that returned no planned meals — drives the empty-state panel.</summary>
    [ObservableProperty]
    private bool _showEmptyState;

    public StatisticiLunareViewModel(ReportRepository reports, ISessionService session)
    {
        _reports = reports;
        _session = session;

        var today = DateTime.Today;
        for (int y = today.Year - 3; y <= today.Year + 1; y++)
            Years.Add(y);
        for (int m = 1; m <= 12; m++)
            Months.Add(new MonthOption(m, MonthName(m)));

        _selectedYear = today.Year;
        _selectedMonth = Months.First(m => m.Number == today.Month);
    }

    public Task LoadAsync() => AfiseazaCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task Afiseaza()
    {
        ClearError();
        if (SelectedMonth is null)
            return;

        IsBusy = true;
        try
        {
            var userId = _session.CurrentUserId;
            int year = SelectedYear, month = SelectedMonth.Number;

            var stats = await _reports.GetMonthlyStatsAsync(userId, year, month);
            Stats = stats;

            SlotCounts.Clear();
            SlotCounts.Add(new SlotCount("Mic dejun", stats.BreakfastCount));
            SlotCounts.Add(new SlotCount("Pranz", stats.LunchCount));
            SlotCounts.Add(new SlotCount("Cina", stats.DinnerCount));
            SlotCounts.Add(new SlotCount("Gustare", stats.SnackCount));
            SlotCounts.Add(new SlotCount("Desert", stats.DessertCount));
            SlotCounts.Add(new SlotCount("Bautura", stats.DrinkCount));

            var topRecipes = await _reports.GetTopRecipesAsync(userId, year, month, topN: 5);
            TopRecipes.Clear();
            foreach (var r in topRecipes)
                TopRecipes.Add(r);

            var topIngredients = await _reports.GetTopIngredientsAsync(userId, year, month, topN: 10);
            TopIngredients.Clear();
            foreach (var i in topIngredients)
                TopIngredients.Add(i);

            HasData = stats.TotalMealsPlanned > 0;
            ShowEmptyState = !HasData;
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
            HasData = false;
            ShowEmptyState = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string MonthName(int month)
    {
        var ro = CultureInfo.GetCultureInfo("ro-RO");
        var name = new DateTime(2000, month, 1).ToString("MMMM", ro);
        return char.ToUpper(name[0], ro) + name[1..];
    }
}

/// <summary>A selectable month: its number (1–12) and Romanian display name.</summary>
public sealed record MonthOption(int Number, string Name);

/// <summary>One row of the per-slot breakdown (e.g. "Pranz" → 7).</summary>
public sealed record SlotCount(string Label, int Count);
