using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.Views.Planificare;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Planificare;

/// <summary>
/// Monthly overview: a Monday-first 6×7 grid of <see cref="DayCellViewModel"/>. Each day shows
/// a chip per planned meal. Clicking an empty day opens the add modal pre-filled with that date;
/// clicking a chip opens that entry for edit. Data comes from <c>sp_GetMonthlyPlan</c>.
/// </summary>
public sealed partial class PlanificareMonthlyViewModel : ViewModelBase, IAsyncLoadable
{
    private const int GridCellCount = 42; // 6 weeks × 7 days

    private readonly IServiceProvider _services;
    private readonly MealPlanRepository _mealPlan;
    private readonly IDialogService _dialog;
    private readonly ISessionService _session;

    public ObservableCollection<DayCellViewModel> Days { get; } = new();
    public string[] WeekdayHeaders { get; } = MealSlots.WeekdayHeaders();

    [ObservableProperty]
    private int _year = DateTime.Today.Year;

    [ObservableProperty]
    private int _month = DateTime.Today.Month;

    [ObservableProperty]
    private string _monthLabel = MealSlots.MonthLabel(DateTime.Today.Year, DateTime.Today.Month);

    public PlanificareMonthlyViewModel(
        IServiceProvider services,
        MealPlanRepository mealPlan,
        IDialogService dialog,
        ISessionService session)
    {
        _services = services;
        _mealPlan = mealPlan;
        _dialog = dialog;
        _session = session;
    }

    public Task LoadAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            MonthLabel = MealSlots.MonthLabel(Year, Month);
            var entries = await _mealPlan.GetMonthlyPlanAsync(_session.CurrentUserId, Year, Month);
            var byDate = entries
                .GroupBy(e => e.PlannedDate.Date)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<MealPlanEntry>)g.ToList());

            var firstOfMonth = new DateTime(Year, Month, 1);
            var gridStart = MealSlots.MondayOf(firstOfMonth);

            Days.Clear();
            for (int i = 0; i < GridCellCount; i++)
            {
                var date = gridStart.AddDays(i);
                Days.Add(new DayCellViewModel
                {
                    Date = date,
                    IsCurrentMonth = date.Month == Month && date.Year == Year,
                    Entries = byDate.TryGetValue(date, out var list) ? list : Array.Empty<MealPlanEntry>(),
                });
            }
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

    [RelayCommand]
    private Task PrevMonth() => Shift(-1);

    [RelayCommand]
    private Task NextMonth() => Shift(+1);

    [RelayCommand]
    private Task Today()
    {
        Year = DateTime.Today.Year;
        Month = DateTime.Today.Month;
        return RefreshAsync();
    }

    private Task Shift(int months)
    {
        var d = new DateTime(Year, Month, 1).AddMonths(months);
        Year = d.Year;
        Month = d.Month;
        return RefreshAsync();
    }

    [RelayCommand]
    private async Task AddOnDay(DayCellViewModel? cell)
    {
        if (cell is null) return;
        var vm = _services.GetRequiredService<PlanMealDialogViewModel>();
        await vm.InitForAddAsync(cell.Date, MealSlots.DefaultSlotId);
        if (_dialog.ShowDialog<PlanMealDialog>(vm))
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task EditEntry(MealPlanEntry? entry)
    {
        if (entry is null) return;
        var vm = _services.GetRequiredService<PlanMealDialogViewModel>();
        await vm.InitForEditAsync(entry);
        if (_dialog.ShowDialog<PlanMealDialog>(vm))
            await RefreshAsync();
    }
}
