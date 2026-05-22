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
using Microsoft.Win32;

namespace MealPrepApp.ViewModels.Rapoarte;

/// <summary>
/// "Lista cumparaturi pentru tiparire" sub-tab. Same computed list as the Ingrediente shopping
/// list (<c>sp_GetShoppingList</c> via <see cref="ShoppingListRepository"/>), presented in a
/// print-first layout with Tipareste + Export Excel.
/// </summary>
public sealed partial class ListaCumparaturiPrintViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly ShoppingListRepository _shopping;
    private readonly ISessionService _session;

    public ObservableCollection<ShoppingListRow> Rows { get; } = new();

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today.AddDays(7);

    [ObservableProperty]
    private bool _hasGenerated;

    public ListaCumparaturiPrintViewModel(ShoppingListRepository shopping, ISessionService session)
    {
        _shopping = shopping;
        _session = session;
    }

    public Task LoadAsync() => GenereazaCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task Genereaza()
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
    private void ExportExcel()
    {
        if (Rows.Count == 0)
            return;

        var save = new SaveFileDialog
        {
            Filter = "Excel|*.xlsx",
            FileName = $"lista-cumparaturi-{StartDate:yyyyMMdd}-{EndDate:yyyyMMdd}.xlsx",
        };
        if (save.ShowDialog() != true) return;

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
            wb.SaveAs(save.FileName);
        }
        catch (IOException ex)
        {
            ErrorMessage = $"Nu am putut salva fisierul: {ex.Message}";
        }
    }
}
