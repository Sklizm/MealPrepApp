using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Planificare;
using Microsoft.Win32;

namespace MealPrepApp.ViewModels.Rapoarte;

/// <summary>
/// "Plan saptamanal pentru tiparire" sub-tab. Pick a week, see a print-friendly 7-day ×
/// 4-slot grid (Mic dejun / Pranz / Cina / Gustare), then Tipareste or Export Excel. Reads
/// <c>sp_GetWeeklyPlan</c> via <see cref="MealPlanRepository"/>. Dessert/Drink entries are not
/// shown here — they have no weekly column (see <see cref="MealSlots.WeeklyColumnIds"/>).
/// </summary>
public sealed partial class PlanSaptamanalPrintViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly MealPlanRepository _mealPlan;
    private readonly ISessionService _session;

    public ObservableCollection<WeeklyPrintRow> Days { get; } = new();

    /// <summary>Any day inside the target week; the grid snaps to that week's Monday.</summary>
    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private string _weekLabel = "";

    [ObservableProperty]
    private bool _hasGenerated;

    public PlanSaptamanalPrintViewModel(MealPlanRepository mealPlan, ISessionService session)
    {
        _mealPlan = mealPlan;
        _session = session;
    }

    public Task LoadAsync() => GenereazaCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task Genereaza()
    {
        ClearError();
        var weekStart = MealSlots.MondayOf(SelectedDate);
        WeekLabel = MealSlots.WeekLabel(weekStart);

        IsBusy = true;
        try
        {
            var entries = await _mealPlan.GetWeeklyPlanAsync(_session.CurrentUserId, weekStart);

            Days.Clear();
            for (int i = 0; i < 7; i++)
            {
                var day = weekStart.AddDays(i);
                var forDay = entries.Where(e => e.PlannedDate.Date == day.Date).ToList();
                Days.Add(new WeeklyPrintRow(
                    MealSlots.DayRowLabel(day),
                    SlotText(forDay, 1),
                    SlotText(forDay, 2),
                    SlotText(forDay, 3),
                    SlotText(forDay, 4)));
            }
            HasGenerated = true;
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
    private async Task PreviousWeek()
    {
        SelectedDate = MealSlots.MondayOf(SelectedDate).AddDays(-7);
        await Genereaza();
    }

    [RelayCommand]
    private async Task NextWeek()
    {
        SelectedDate = MealSlots.MondayOf(SelectedDate).AddDays(7);
        await Genereaza();
    }

    [RelayCommand]
    private void ExportExcel()
    {
        if (Days.Count == 0)
            return;

        var weekStart = MealSlots.MondayOf(SelectedDate);
        var save = new SaveFileDialog
        {
            Filter = "Excel|*.xlsx",
            FileName = $"plan-saptamanal-{weekStart:yyyyMMdd}.xlsx",
        };
        if (save.ShowDialog() != true) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Plan saptamanal");

            ws.Cell(1, 1).Value = "Zi";
            ws.Cell(1, 2).Value = "Mic dejun";
            ws.Cell(1, 3).Value = "Pranz";
            ws.Cell(1, 4).Value = "Cina";
            ws.Cell(1, 5).Value = "Gustare";
            ws.Range(1, 1, 1, 5).Style.Font.Bold = true;

            var row = 2;
            foreach (var d in Days)
            {
                ws.Cell(row, 1).Value = d.Day;
                ws.Cell(row, 2).Value = d.Breakfast;
                ws.Cell(row, 3).Value = d.Lunch;
                ws.Cell(row, 4).Value = d.Dinner;
                ws.Cell(row, 5).Value = d.Snack;
                ws.Row(row).Style.Alignment.WrapText = true;
                row++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(save.FileName);
        }
        catch (IOException ex)
        {
            ErrorMessage = $"Nu am putut salva fisierul: {ex.Message}";
        }
    }

    /// <summary>Newline-joined recipe titles planned for one day + meal slot.</summary>
    private static string SlotText(IEnumerable<MealPlanEntry> dayEntries, int categoryId) =>
        string.Join(Environment.NewLine,
            dayEntries.Where(e => e.CategoryID == categoryId).Select(e => e.RecipeTitle));
}

/// <summary>One day's row in the printable weekly grid: a label plus the four meal-slot cells.</summary>
public sealed record WeeklyPrintRow(string Day, string Breakfast, string Lunch, string Dinner, string Snack);
