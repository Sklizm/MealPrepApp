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

namespace MealPrepApp.ViewModels.Rapoarte;

/// <summary>
/// "Printable weekly plan" sub-tab. Pick a week, see a print-friendly 7-day ×
/// 4-slot grid (Mic dejun / Pranz / Cina / Gustare), then Tipareste or Export Excel. Reads
/// <c>sp_GetWeeklyPlan</c> via <see cref="MealPlanRepository"/>. Dessert/Drink entries are not
/// shown here — they have no weekly column (see <see cref="MealSlots.WeeklyColumnIds"/>).
/// </summary>
public sealed partial class PlanSaptamanalPrintViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly MealPlanRepository _mealPlan;
    private readonly ISessionService _session;
    private readonly IFilePickerService _filePicker;

    public ObservableCollection<WeeklyPrintRow> Days { get; } = new();

    /// <summary>Any day inside the target week; the grid snaps to that week's Monday.</summary>
    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [LocalizedProperty]
    public string WeekLabel { get => MealSlots.WeekLabel(MealSlots.MondayOf(SelectedDate)); private set => OnPropertyChanged(); }

    [ObservableProperty]
    private bool _hasGenerated;

    public PlanSaptamanalPrintViewModel(
        MealPlanRepository mealPlan,
        ISessionService session,
        IFilePickerService filePicker)
    {
        _mealPlan = mealPlan;
        _session = session;
        _filePicker = filePicker;
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
                    SlotText(forDay, 4)) { Date = day });
            }
            HasGenerated = true;
        }
        catch (AppDbException ex)
        {
            ErrorContent = ex.LocalizedMessage;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            ErrorContent = L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog");
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
    private async Task ExportExcel()
    {
        if (Days.Count == 0)
            return;

        var weekStart = MealSlots.MondayOf(SelectedDate);
        var defaultFileName = $"weekly-plan-{weekStart:yyyyMMdd}.xlsx";
        var savePath = await _filePicker.SaveFileDialogAsync(L.Text("Text.ExportWeeklyPlan"), defaultFileName, "Excel|*.xlsx");
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet(L.Text("Text.WeeklyPlan370"));

            ws.Cell(1, 1).Value = L.Text("Text.Day");
            ws.Cell(1, 2).Value = L.Text("Text.Breakfast");
            ws.Cell(1, 3).Value = L.Text("Text.Lunch");
            ws.Cell(1, 4).Value = L.Text("Text.Dinner");
            ws.Cell(1, 5).Value = L.Text("Text.Snack");
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
            wb.SaveAs(savePath);
        }
        catch (IOException ex)
        {
            AppDiagnostics.Record(ex);
            ErrorContent = L.Message("Error.SaveFile");
        }
    }

    /// <summary>Newline-joined recipe titles planned for one day + meal slot.</summary>
    private static string SlotText(IEnumerable<MealPlanEntry> dayEntries, int categoryId) =>
        string.Join(Environment.NewLine,
            dayEntries.Where(e => e.CategoryID == categoryId).Select(e => e.RecipeTitle));
}

/// <summary>One day's row in the printable weekly grid: a label plus the four meal-slot cells.</summary>
public sealed class WeeklyPrintRow(string day, string breakfast, string lunch, string dinner, string snack) : LocalizedObservableObject
{
    public DateTime? Date { get; init; }
    [LocalizedProperty]
    public string Day => Date is {} date ? MealSlots.DayRowLabel(date) : day;
    public string Breakfast { get; } = breakfast;
    public string Lunch { get; } = lunch;
    public string Dinner { get; } = dinner;
    public string Snack { get; } = snack;
}
