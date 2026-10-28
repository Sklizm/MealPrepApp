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

/// <summary>Saved shopping snapshots with category/checklist views, explicit pantry import and exports.</summary>
public sealed partial class ShoppingListViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly ShoppingListRepository _shopping;
    private readonly ISessionService _session;
    private readonly IDialogService _dialog;
    private readonly IFilePickerService _filePicker;

    public ObservableCollection<ShoppingSnapshot> Snapshots { get; }=new();
    public ObservableCollection<ShoppingGroup> Groups { get; }=new();
    [ObservableProperty] private ShoppingSnapshot? _selectedSnapshot;
    [ObservableProperty] private string _purchaseFilter="Toate";
    public IReadOnlyList<string> PurchaseFilters { get; }=new[]{"Toate","De cumpărat","Cumpărate"};
    public string ProgressText => $"{Rows.Count(r=>r.IsPurchased)} din {Rows.Count} cumpărate";
    public string PlainText => string.Join(Environment.NewLine,Rows.Select(r=>$"[{(r.IsPurchased ? "x" : " ")}] {r.IngredientName}: {r.ToBuyQty:0.##} {r.UnitAbbreviation}"));
    partial void OnPurchaseFilterChanged(string value)=>Regroup();
    partial void OnSelectedSnapshotChanged(ShoppingSnapshot? value) { if(!_selecting) _ = RunAsync(LoadSelectedAsync); }
    private bool _selecting;
    private void Regroup()
    {
        Groups.Clear();
        foreach(var group in Rows.Where(r=>PurchaseFilter=="Toate" || (PurchaseFilter=="Cumpărate" ? r.IsPurchased : !r.IsPurchased)).GroupBy(r=>r.CategoryName)) Groups.Add(new ShoppingGroup(group.Key,group));
        OnPropertyChanged(nameof(ProgressText)); OnPropertyChanged(nameof(PlainText));
    }
    private async Task LoadSelectedAsync()
    {
        if(SelectedSnapshot is null) {Rows.Clear();Regroup();return;}
        StartDate=SelectedSnapshot.StartDate;EndDate=SelectedSnapshot.EndDate;
        var rows=await _shopping.GetSnapshotItemsAsync(_session.CurrentUserId,SelectedSnapshot.SnapshotID);
        Rows.Clear();foreach(var row in rows) Rows.Add(row);
        HasGenerated=true;Regroup();
    }
    private async Task RunAsync(Func<Task> operation)
    {
        if(IsBusy) return;
        ClearError(); IsBusy=true;
        try {await operation();} catch(AppDbException ex) {ErrorMessage=ex.FriendlyMessage;} catch(Exception ex) {AppDiagnostics.Record(ex); ErrorMessage="Lista nu a putut fi actualizată. Încearcă din nou.";} finally {IsBusy=false;Regroup();}
    }
    [RelayCommand] private Task TogglePurchased(ShoppingListRow? row)=>row is null ? Task.CompletedTask : RunAsync(async()=>{await _shopping.SetPurchasedAsync(_session.CurrentUserId,row.ItemID,!row.IsPurchased);await LoadSelectedAsync();});
    [RelayCommand] private Task ImportItem(ShoppingListRow? row)=>row is null || !row.CanImport ? Task.CompletedTask : RunAsync(async()=>
    {
        if(!await _dialog.ConfirmAsync("Adaugă în frigider",$"Adaugi {row.ToBuyQty:0.##} {row.UnitAbbreviation} {row.IngredientName} în frigider? Acest articol poate fi importat o singură dată.")) return;
        await _shopping.ImportItemAsync(_session.CurrentUserId,row.ItemID);await LoadSelectedAsync();Notification="Cantitatea a fost adăugată în frigider.";
    });
    [RelayCommand] private Task Retry()=>LoadAsync();
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

    public Task LoadAsync() => RunAsync(async()=>
    {
        var selected=SelectedSnapshot?.SnapshotID;
        var snapshots=await _shopping.GetSnapshotsAsync(_session.CurrentUserId);
        _selecting=true;try {Snapshots.Clear();foreach(var snapshot in snapshots) Snapshots.Add(snapshot);SelectedSnapshot=Snapshots.FirstOrDefault(s=>s.SnapshotID==selected) ?? Snapshots.FirstOrDefault();}finally{_selecting=false;}
        await LoadSelectedAsync();
    });

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
            var id=await _shopping.CreateSnapshotAsync(_session.CurrentUserId,StartDate,EndDate);
            var snapshots=await _shopping.GetSnapshotsAsync(_session.CurrentUserId);
            _selecting=true;try {Snapshots.Clear();foreach(var snapshot in snapshots) Snapshots.Add(snapshot);SelectedSnapshot=Snapshots.FirstOrDefault(s=>s.SnapshotID==id);}finally{_selecting=false;}
            await LoadSelectedAsync();
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
    private async Task ExportExcel()
    {
        if (Rows.Count == 0)
        {
            await _dialog.ShowInfoAsync("Nimic de exportat", "Genereaza intai lista de cumparaturi.");
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

            Notification="Lista a fost exportată.";
        }
        catch (IOException ex)
        {
            await _dialog.ShowErrorAsync($"Nu am putut salva fisierul: {ex.Message}");
        }
    }

}

public sealed class ShoppingGroup(string name,IEnumerable<ShoppingListRow> items):ObservableCollection<ShoppingListRow>(items)
{
    public string Name { get; }=name;
}
