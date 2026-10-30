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

namespace MealPrepApp.ViewModels.Rapoarte;

/// <summary>
/// "Printable shopping list" sub-tab. Same computed list as the Ingrediente shopping
/// list (<c>sp_GetShoppingList</c> via <see cref="ShoppingListRepository"/>), presented in a
/// print-first layout with Tipareste + Export Excel.
/// </summary>
public sealed partial class ListaCumparaturiPrintViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly ShoppingListRepository _shopping;
    private readonly ISessionService _session;
    private readonly IFilePickerService _filePicker;

    public ObservableCollection<ShoppingListRow> Rows { get; } = new();

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today.AddDays(7);

    [ObservableProperty]
    private bool _hasGenerated;

    public ListaCumparaturiPrintViewModel(
        ShoppingListRepository shopping,
        ISessionService session,
        IFilePickerService filePicker)
    {
        _shopping = shopping;
        _session = session;
        _filePicker = filePicker;
    }

    public Task LoadAsync() => GenereazaCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task Genereaza()
    {
        ClearError();

        if (EndDate < StartDate)
        {
            ErrorContent = L.Message("Text.TheEndDateMustBeAfterTheStartDate368");
            return;
        }

        IsBusy = true;
        try
        {
            var rows = await _shopping.GetShoppingListAsync(_session.CurrentUserId, StartDate, EndDate);
            Rows.Clear();
            foreach (var row in rows.OrderBy(r => r.IngredientName))
                Rows.Add(row);
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
    private async Task ExportExcel()
    {
        if (Rows.Count == 0)
            return;

        var defaultFileName = $"shopping-list-{StartDate:yyyyMMdd}-{EndDate:yyyyMMdd}.xlsx";
        var savePath = await _filePicker.SaveFileDialogAsync(L.Text("Text.ExportShoppingList"), defaultFileName, "Excel|*.xlsx");
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet(L.Text("Text.ShoppingList351"));

            ws.Cell(1, 1).Value = L.Text("Text.Ingredient165");
            ws.Cell(1, 2).Value = L.Text("Text.Required");
            ws.Cell(1, 3).Value = L.Text("Text.InPantry");
            ws.Cell(1, 4).Value = L.Text("Text.ToBuy");
            ws.Cell(1, 5).Value = L.Text("Text.Unit169");
            ws.Range(1, 1, 1, 5).Style.Font.Bold = true;

            var row = 2;
            foreach (var item in Rows)
            {
                ws.Cell(row, 1).Value = item.IngredientName;
                ws.Cell(row, 2).Value = item.NeededQty;
                ws.Cell(row, 3).Value = item.OnHandQty;
                ws.Cell(row, 4).Value = item.ToBuyQty;
                ws.Cell(row, 5).Value = item.UnitAbbreviation;
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
}
