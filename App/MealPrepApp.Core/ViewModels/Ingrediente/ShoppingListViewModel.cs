using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels.Ingrediente;

/// <summary>Saved shopping snapshots with category/checklist views, explicit pantry import and exports.</summary>
public sealed partial class ShoppingListViewModel : ViewModelBase, IAsyncLoadable, IDisposable
{
    private readonly ShoppingListRepository _shopping;
    private readonly ISessionService _session;
    private readonly IDialogService _dialog;
    private readonly IFilePickerService _filePicker;
    private readonly IServiceProvider? _services;
    private readonly Dictionary<int, (Guid Operation, ShoppingChange Change)> _pending = new();
    private readonly Dictionary<int, Guid> _imports = new();
    private int _savingCount;
    [LocalizedProperty] public string EmptyText => L.Text(SelectedSnapshot is null ? "UX.ShoppingEmpty" :
        PurchaseFilter == PurchaseFilterIds.ToBuy && Rows.Count > 0 ? "UX.ShoppingDone" : "UX.ShoppingNoItems");
    private void NotifyProgress() { OnPropertyChanged(nameof(ProgressText)); OnPropertyChanged(nameof(PlainText)); OnPropertyChanged(nameof(EmptyText)); }
    [RelayCommand] private Task ThisWeek() => GenerateWeek(0);
    [RelayCommand] private Task NextWeek() => GenerateWeek(7);
    private Task GenerateWeek(int offset) { StartDate = DateTime.Today.AddDays(-((7 + (int)DateTime.Today.DayOfWeek - (int)DayOfWeek.Monday) % 7) + offset); EndDate = StartDate.AddDays(6); return Generate(); }


    public ObservableCollection<ShoppingSnapshot> Snapshots { get; }=new();
    public ObservableCollection<ShoppingGroup> Groups { get; }=new();
    [ObservableProperty] private ShoppingSnapshot? _selectedSnapshot;
    [ObservableProperty] private string _purchaseFilter=PurchaseFilterIds.ToBuy;
    public IReadOnlyList<string> PurchaseFilters { get; }=new[]{PurchaseFilterIds.All,PurchaseFilterIds.ToBuy,PurchaseFilterIds.Purchased};
    [LocalizedProperty]
    public string ProgressText => L.Text("Shopping.Progress", Rows.Count(r=>r.IsPurchased), Rows.Count);
    public string PlainText => L.Text("Text.ShoppingList") + $" ({StartDate:d} – {EndDate:d})" + Environment.NewLine + ProgressText + Environment.NewLine + string.Join(Environment.NewLine,Rows.Select(r=>$"[{(r.IsPurchased ? "x" : " ")}] {r.IngredientName}: {r.ToBuyQty:0.##} {r.UnitAbbreviation}"));
    partial void OnPurchaseFilterChanged(string value)=>Regroup();
    partial void OnSelectedSnapshotChanged(ShoppingSnapshot? value)
    {
        if (!_selecting) _ = ReadAsync(false);
    }
    private bool _selecting;
    private bool _disposed;
    private int _readVersion;
    private CancellationTokenSource? _readCts;
    [ObservableProperty] private bool _isMutating;
    private bool _isLoading;
    private bool CanMutate() => !IsBusy && !_disposed;
    private void UpdateBusy()
    {
        IsBusy = _isLoading || IsMutating;
        GenerateCommand.NotifyCanExecuteChanged(); TogglePurchasedCommand.NotifyCanExecuteChanged();
        ImportItemCommand.NotifyCanExecuteChanged(); ExportExcelCommand.NotifyCanExecuteChanged();
        ImportAllCommand.NotifyCanExecuteChanged(); AddGroceryCommand.NotifyCanExecuteChanged(); EditGroceryCommand.NotifyCanExecuteChanged();
    }
    public override void Dispose()
    {
        ReleaseLocalization();
        if (_disposed) return;
        _disposed = true; ++_readVersion; _readCts?.Cancel(); _readCts?.Dispose();
    }
    private void Regroup()
    {
        Groups.Clear();
        foreach(var group in Rows.Where(r=>PurchaseFilter==PurchaseFilterIds.All || (PurchaseFilter==PurchaseFilterIds.Purchased ? r.IsPurchased : !r.IsPurchased)).GroupBy(r=>r.CategoryName)) Groups.Add(new ShoppingGroup(group.Key,group));
        NotifyProgress();
    }
    private async Task ReadAsync(bool refreshSnapshots, int? selectId = null)
    {
        if (_disposed) return;
        var version = ++_readVersion;
        _readCts?.Cancel(); _readCts?.Dispose();
        _readCts = new CancellationTokenSource(); var token = _readCts.Token;
        var userId = _session.CurrentUserId;
        var snapshot = SelectedSnapshot;
        _isLoading = true; UpdateBusy(); ClearError();
        try
        {
            if (refreshSnapshots)
            {
                var snapshots = await _shopping.GetSnapshotsAsync(userId, token);
                if (version != _readVersion || _disposed) return;
                snapshot = snapshots.FirstOrDefault(s => s.SnapshotID == (selectId ?? snapshot?.SnapshotID)) ?? snapshots.FirstOrDefault();
                _selecting = true;
                try { Snapshots.Clear(); foreach (var item in snapshots) Snapshots.Add(item); SelectedSnapshot = snapshot; }
                finally { _selecting = false; }
            }
            // Capture the ID before the await; even a repository that ignores cancellation cannot replace a newer selection.
            var id = snapshot?.SnapshotID;
            Rows.Clear(); HasGenerated = false; Regroup();
            if (snapshot is null) return;
            StartDate = snapshot.StartDate; EndDate = snapshot.EndDate;
            var rows = await _shopping.GetSnapshotItemsAsync(userId, snapshot.SnapshotID, token);
            if (version != _readVersion || _disposed || SelectedSnapshot?.SnapshotID != id) return;
            foreach (var row in rows) { row.SnapshotID = snapshot.SnapshotID; Rows.Add(row); }
            HasGenerated = true; Regroup();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (version == _readVersion && !_disposed)
            {
                AppDiagnostics.Record(ex);
                ErrorContent = ex is AppDbException db ? db.LocalizedMessage : L.Message("Text.TheListCouldNotBeLoadedTryAgain");
            }
        }
        finally { if (version == _readVersion && !_disposed) { _isLoading = false; UpdateBusy(); } }
    }
    private async Task MutateAsync(Func<Task> operation)
    {
        if (!CanMutate()) return;
        IsMutating = true; UpdateBusy(); ClearError();
        try { await operation(); }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!_disposed) { AppDiagnostics.Record(ex); ErrorContent = ex is AppDbException db ? db.LocalizedMessage : L.Message("Text.TheListCouldNotBeUpdatedTryAgain"); }
        }
        finally { if (!_disposed) { IsMutating = false; UpdateBusy(); } }
    }
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task TogglePurchased(ShoppingListRow? row)
    {
        if (row is null || !Rows.Contains(row) || row.IsSaving || _isLoading || _disposed) return;
        if (!row.SaveFailed) {
            row.IsPurchased = !row.IsPurchased;
            _pending[row.ItemID] = (Guid.NewGuid(), ShoppingChange.From(row));
        }
        await SaveCheckAsync(row);
    }
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task RetryItem(ShoppingListRow? row) => row is null || !row.SaveFailed ? Task.CompletedTask : SaveCheckAsync(row);
    private async Task SaveCheckAsync(ShoppingListRow row)
    {
        if (!_pending.TryGetValue(row.ItemID, out var pending) || row.IsSaving || _disposed) return;
        var user = _session.CurrentUserId;
        row.IsSaving = true; row.SaveFailed = false; _savingCount++; IsMutating = true; UpdateBusy(); ClearError(); NotifyProgress();
        try {
            var result = await _shopping.ChangeItemAsync(user, pending.Operation, pending.Change);
            row.Version = result.Version; _pending.Remove(row.ItemID);
        }
        catch (Exception ex) {
            AppDiagnostics.Record(ex); row.SaveFailed = true; row.HasConflict = ex is AppDbException { ErrorNumber: 50004 };
            ErrorContent = ex is AppDbException db ? db.LocalizedMessage : L.Message("UX.CheckRetry");
        }
        finally { row.IsSaving = false; _savingCount--; IsMutating = _savingCount > 0; UpdateBusy(); NotifyProgress(); }
        // Keep the existing rows and visual containers while checking items. Reapply the
        // filter only when the user changes it or refreshes, preserving keyboard focus.
    }
    private async Task ImportReviewed(ShoppingListRow row)
    {
        if (!_imports.TryGetValue(row.ItemID, out var operation)) _imports[row.ItemID] = operation = Guid.NewGuid();
        var result = await _shopping.ImportPurchaseAsync(_session.CurrentUserId, operation, row.ItemID, row.Version);
        row.Version = result.Version; row.IsImported = true; _imports.Remove(row.ItemID);
    }
    [RelayCommand(CanExecute = nameof(CanMutate))]
    private Task ImportItem(ShoppingListRow? row) => row is null || !row.CanImport || !Rows.Contains(row) ? Task.CompletedTask : MutateAsync(async () =>
    {
        if (!await _dialog.ConfirmAsync(L.Message("Text.AddToPantry"), L.Message("Shopping.ImportPrompt", row.ActualQuantity, row.ActualUnit, row.IngredientName))) return;
        await ImportReviewed(row);
        if (!_disposed) NotificationContent = L.Message("Text.QuantityAddedToPantry");
    });
    [RelayCommand(CanExecute = nameof(CanMutate))]
    private Task ImportAll() => MutateAsync(async () => {
        var eligible = Rows.Where(r => r.CanImport).ToArray();
        if (eligible.Length == 0) return;
        var review = string.Join(Environment.NewLine, eligible.Select(r => $"{r.IngredientName}: {r.ActualQuantity:0.####} {r.ActualUnit}"));
        if (!await _dialog.ConfirmAsync(L.Message("Text.AddToPantry"), L.Message("UX.BulkImport", review))) return;
        foreach (var row in eligible) await ImportReviewed(row);
        NotificationContent = L.Message("Text.QuantityAddedToPantry");
    });
    [RelayCommand(CanExecute = nameof(CanMutate))] private Task AddGrocery() => EditGrocery(null);
    [RelayCommand(CanExecute = nameof(CanMutate))]
    private Task EditGrocery(ShoppingListRow? row) => MutateAsync(async () => {
        if (_services is null) return;
        if (SelectedSnapshot is null) { var id = await _shopping.CreateSnapshotAsync(_session.CurrentUserId, StartDate, EndDate); await ReadAsync(true, id); }
        if (SelectedSnapshot is null) return;
        var editor = new ShoppingItemDialogViewModel(_shopping, _services.GetRequiredService<IngredientRepository>(),
            _services.GetRequiredService<LookupRepository>(), _session, SelectedSnapshot.SnapshotID, row);
        await editor.LoadAsync();
        if (await _dialog.ShowDialogAsync(editor)) await ReadAsync(false);
    });
    [RelayCommand] private async Task ResolveItem(ShoppingListRow? row) {
        if (row is null || row.IsSaving || !row.HasConflict) return;
        if (!await _dialog.ConfirmAsync(L.Message("UX.ResolveConflict"), L.Message("UX.UseServerCopy"))) return;
        _pending.Remove(row.ItemID); await ReadAsync(false);
    }
    [RelayCommand] private async Task Retry() {
        var failed = Rows.Where(r => r.SaveFailed && !r.HasConflict).ToArray();
        if (failed.Length == 0) { if (Rows.Any(r => r.HasConflict)) return; await LoadAsync(); }
        else foreach (var row in failed) await SaveCheckAsync(row);
    }
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
        IFilePickerService filePicker, IServiceProvider? services = null)
    {
        _shopping = shopping;
        _session = session;
        _dialog = dialog;
        _filePicker = filePicker; _services = services;
        Rows.CollectionChanged += (_, _) => { if (!_isLoading) Regroup(); };
    }

    public Task LoadAsync() => ReadAsync(true);

    [RelayCommand(CanExecute = nameof(CanMutate))]
    private Task Generate()
    {
        ClearError();
        if (EndDate < StartDate)
        {
            ErrorContent = L.Message("Text.TheEndDateMustBeAfterTheStartDate");
            return Task.CompletedTask;
        }
        var userId = _session.CurrentUserId; var start = StartDate; var end = EndDate;
        return MutateAsync(async () =>
        {
            var id = await _shopping.CreateSnapshotAsync(userId, start, end);
            await ReadAsync(true, id);
        });
    }

    [RelayCommand(CanExecute = nameof(CanMutate))]
    private async Task ExportExcel()
    {
        if (Rows.Count == 0)
        {
            await _dialog.ShowInfoAsync(L.Message("Text.NothingToExport"), L.Message("Text.GenerateAShoppingListFirst"));
            return;
        }

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

            NotificationContent = L.Message("Text.ListExported");
        }
        catch (IOException ex)
        {
            AppDiagnostics.Record(ex);
            await _dialog.ShowErrorAsync(L.Message("Error.SaveFile"));
        }
    }

}

public sealed class ShoppingGroup(string name,IEnumerable<ShoppingListRow> items):ObservableCollection<ShoppingListRow>(items)
{
    public string Name { get; }=name;
}
