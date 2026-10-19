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

namespace MealPrepApp.ViewModels.Ingrediente;

/// <summary>
/// "Lista de cumparaturi" sub-view. Pick a date range, hit Genereaza, see what to buy.
/// Backed by <c>sp_GetShoppingList(@UserID, @StartDate, @EndDate)</c>; results are computed
/// per call (never stored). Export Excel + Print are toolbar actions.
/// </summary>
public sealed partial class ShoppingListViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly ShoppingListRepository _shopping;
    private readonly ISessionService _session;
    private readonly IDialogService _dialog;
    private readonly IFilePickerService _filePicker;

    public ObservableCollection<ShoppingListRow> Rows { get; } = new();

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today.AddDays(7);

    [ObservableProperty]
    private bool _hasGenerated;

    public ShoppingListViewModel(
        ShoppingListRepository shopping,
        ISessionService session,
        IDialogService dialog,
        IFilePickerService filePicker)
    {
        _shopping = shopping;
        _session = session;
        _dialog = dialog;
        _filePicker = filePicker;
    }

    public Task LoadAsync() => Task.CompletedTask;

    [RelayCommand]
    private async Task Generate()
    {
        ClearError();

        if (EndDate < StartDate)
        {
            ErrorMessage = "Data de sfarsit trebuie sa fie dupa data de inceput.";
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
            ErrorMessage = ex.FriendlyMessage;
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
        {
            _dialog.ShowInfo("Nimic de exportat", "Genereaza intai lista de cumparaturi.");
            return;
        }

        var defaultFileName = $"lista-cumparaturi-{StartDate:yyyyMMdd}-{EndDate:yyyyMMdd}.xlsx";
        var savePath = await _filePicker.SaveFileDialogAsync("Exporta lista de cumparaturi", defaultFileName, "Excel|*.xlsx");
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Lista cumparaturi");

            ws.Cell(1, 1).Value = "Ingredient";
            ws.Cell(1, 2).Value = "Necesar";
            ws.Cell(1, 3).Value = "In frigider";
            ws.Cell(1, 4).Value = "De cumparat";
            ws.Cell(1, 5).Value = "Unitate";
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

            _dialog.ShowInfo("Export reusit", $"Lista a fost salvata in:\n{savePath}");
        }
        catch (IOException ex)
        {
            _dialog.ShowError($"Nu am putut salva fisierul: {ex.Message}");
        }
    }

}
