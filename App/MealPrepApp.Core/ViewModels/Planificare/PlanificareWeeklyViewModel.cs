using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
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

    public WeekCellViewModel DefaultCell=>new(){Date=WeekStart,CategoryId=MealSlots.DefaultSlotId};
    public ObservableCollection<MealPlanEntry> AgendaEntries {get;}=new();
    public ObservableCollection<MealPlanEntry> SupplementaryEntries {get;}=new();
    [ObservableProperty] private bool _agendaView;
    [ObservableProperty] private DateTime _copyTarget=MealSlots.MondayOf(DateTime.Today).AddDays(7);
    [RelayCommand] private Task Retry()=>RefreshAsync();
    [RelayCommand] private async Task DuplicateEntry(MealPlanEntry? entry)
    {
        if(entry is null) return;
        try {
            var vm=_services.GetRequiredService<PlanMealDialogViewModel>();
            await vm.InitForAddAsync(entry.PlannedDate.AddDays(1),entry.CategoryID,new RecipeListItem{RecipeID=entry.RecipeID,Title=entry.RecipeTitle,Servings=entry.Servings,CategoryID=entry.CategoryID});
            vm.Servings=entry.Servings;vm.Notes=entry.Notes;
            if(await _dialog.ShowDialogAsync(vm)) await RefreshAsync();
        } catch(Exception ex) {AppDiagnostics.Record(ex);ErrorMessage="Masa nu a putut fi duplicată.";}
    }
    public async Task MoveEntryAsync(MealPlanEntry entry,DateTime date,int categoryId)
    {
        try {await _mealPlan.UpdatePlannedMealAsync(entry.MealPlanEntryID,_session.CurrentUserId,categoryId,date,entry.Servings,entry.Notes);await RefreshAsync();}
        catch(AppDbException ex) {ErrorMessage=ex.FriendlyMessage;}
        catch(Exception ex){AppDiagnostics.Record(ex);ErrorMessage="Masa nu a putut fi mutată. Încearcă din nou.";}
    }
    [RelayCommand] private async Task CopyWeek()
    {
        if(IsBusy) return;
        ClearError();IsBusy=true;
        try
        {
            var target=MealSlots.MondayOf(CopyTarget);
            var preview=await _mealPlan.PreviewWeekCopyAsync(_session.CurrentUserId,WeekStart,target);
            if(preview.Count==0){Notification="Săptămâna sursă nu conține mese.";return;}
            var details=string.Join("\n",preview.Select(p=>$"{p.PlannedDate:dd.MM} • {p.CategoryName} • {p.RecipeTitle} ({p.Servings?.ToString() ?? "porții implicite"}){(p.IsDuplicate ? " — există deja, se omite" : "")}"));
            if(!await _dialog.ConfirmAsync("Previzualizare copiere săptămână",details+"\n\nMesele existente se păstrează. Copiezi aceste mese?")) return;
            var count=await _mealPlan.CopyWeekAsync(_session.CurrentUserId,WeekStart,target,preview[0].PreviewToken);
            Notification=$"{count} mese copiate. Mesele existente au fost păstrate.";
        }
        catch(AppDbException ex){ErrorMessage=ex.FriendlyMessage;}
        catch(Exception ex){AppDiagnostics.Record(ex);ErrorMessage="Săptămâna nu a putut fi copiată. Reîncearcă previzualizarea.";}
        finally {IsBusy=false;}
    }
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

            AgendaEntries.Clear(); SupplementaryEntries.Clear();
            foreach(var entry in entries.OrderBy(e=>e.PlannedDate).ThenBy(e=>e.CategoryID))
            {AgendaEntries.Add(entry);if(!MealSlots.WeeklyColumnIds.Contains(entry.CategoryID)) SupplementaryEntries.Add(entry);}
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
        if (await _dialog.ShowDialogAsync(vm))
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task EditEntry(MealPlanEntry? entry)
    {
        if (entry is null) return;
        var vm = _services.GetRequiredService<PlanMealDialogViewModel>();
        await vm.InitForEditAsync(entry);
        if (await _dialog.ShowDialogAsync(vm))
            await RefreshAsync();
    }
}
