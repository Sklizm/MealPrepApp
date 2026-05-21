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
/// Weekly grid: 7 day rows × 4 meal-slot columns (Breakfast / Lunch / Dinner / Snack). Each
/// cell lists its planned recipes; clicking a recipe edits that entry, the empty area adds one
/// pre-filled with the cell's date + slot. Data comes from <c>sp_GetWeeklyPlan</c>.
/// </summary>
public sealed partial class PlanificareWeeklyViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly IServiceProvider _services;
    private readonly MealPlanRepository _mealPlan;
    private readonly LookupRepository _lookups;
    private readonly IDialogService _dialog;
    private readonly ISessionService _session;

    public ObservableCollection<WeekRowViewModel> Rows { get; } = new();

    /// <summary>The four slot column headers, resolved from the loaded categories.</summary>
    public ObservableCollection<string> SlotHeaders { get; } = new();

    [ObservableProperty]
    private DateTime _weekStart = MealSlots.MondayOf(DateTime.Today);

    [ObservableProperty]
    private string _weekLabel = MealSlots.WeekLabel(MealSlots.MondayOf(DateTime.Today));

    public PlanificareWeeklyViewModel(
        IServiceProvider services,
        MealPlanRepository mealPlan,
        LookupRepository lookups,
        IDialogService dialog,
        ISessionService session)
    {
        _services = services;
        _mealPlan = mealPlan;
        _lookups = lookups;
        _dialog = dialog;
        _session = session;
    }

    public async Task LoadAsync()
    {
        await LoadSlotHeadersAsync();
        await RefreshAsync();
    }

    private async Task LoadSlotHeadersAsync()
    {
        if (SlotHeaders.Count > 0) return;
        try
        {
            var categories = await _lookups.GetCategoriesAsync();
            var byId = categories.ToDictionary(c => c.CategoryID, c => c.Name);
            foreach (var id in MealSlots.WeeklyColumnIds)
                SlotHeaders.Add(byId.TryGetValue(id, out var name) ? name : $"Slot {id}");
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
        }
    }

    private async Task RefreshAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            WeekLabel = MealSlots.WeekLabel(WeekStart);
            var entries = await _mealPlan.GetWeeklyPlanAsync(_session.CurrentUserId, WeekStart);

            // (date, slot) → entries
            var byCell = entries
                .GroupBy(e => (e.PlannedDate.Date, e.CategoryID))
                .ToDictionary(g => g.Key, g => (IReadOnlyList<MealPlanEntry>)g.ToList());

            Rows.Clear();
            for (int day = 0; day < 7; day++)
            {
                var date = WeekStart.AddDays(day);
                var cells = MealSlots.WeeklyColumnIds.Select(slot => new WeekCellViewModel
                {
                    Date = date,
                    CategoryId = slot,
                    Entries = byCell.TryGetValue((date, slot), out var list) ? list : Array.Empty<MealPlanEntry>(),
                }).ToList();

                Rows.Add(new WeekRowViewModel
                {
                    Date = date,
                    DayLabel = MealSlots.DayRowLabel(date),
                    Cells = cells,
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
    private Task PrevWeek() => Shift(-7);

    [RelayCommand]
    private Task NextWeek() => Shift(+7);

    [RelayCommand]
    private Task ThisWeek()
    {
        WeekStart = MealSlots.MondayOf(DateTime.Today);
        return RefreshAsync();
    }

    private Task Shift(int days)
    {
        WeekStart = WeekStart.AddDays(days);
        return RefreshAsync();
    }

    [RelayCommand]
    private async Task AddInCell(WeekCellViewModel? cell)
    {
        if (cell is null) return;
        var vm = _services.GetRequiredService<PlanMealDialogViewModel>();
        await vm.InitForAddAsync(cell.Date, cell.CategoryId);
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
