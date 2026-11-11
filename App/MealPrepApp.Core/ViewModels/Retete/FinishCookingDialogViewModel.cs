using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
namespace MealPrepApp.ViewModels.Retete;
public sealed partial class ReviewedStockLine(InventoryBatch batch, decimal quantity) : ObservableObject
{
    public InventoryBatch Batch { get; } = batch;
    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private decimal _quantity = quantity;
}
public sealed partial class FinishCookingDialogViewModel(CookingSession cooking, CookingBatchRepository batches,
    InventoryRepository inventory, ISessionService session) : ViewModelBase
{
    public ObservableCollection<ReviewedStockLine> Deductions { get; } = new();
    public ObservableCollection<IngredientShortage> Shortages { get; } = new();
    [ObservableProperty] private decimal _producedPortions = cooking.Servings;
    [ObservableProperty] private decimal _consumedPortions;
    [ObservableProperty] private DateTime? _expiresOn;
    [ObservableProperty] private bool _includeExpired;
    [ObservableProperty] private bool _acceptIncompleteDeductions;
    [ObservableProperty] private bool _needsReview;
    private CookingBatch? _batch;
    private Guid _operation = cooking.FinishOperation;
    public event EventHandler? SaveSucceeded;
    partial void OnIncludeExpiredChanged(bool value) => _ = LoadAsync();
    public async Task LoadAsync() {
        ClearError(); IsBusy = true;
        try {
            if (cooking.Recipe is null) return;
            var stock = await inventory.GetBatchesAsync(session.CurrentUserId);
            var maps = await inventory.GetUnitMapAsync();
            if (cooking.CookingBatchID.HasValue) _batch = (await batches.GetCookingBatchesAsync(session.CurrentUserId)).FirstOrDefault(b => b.CookingBatchID == cooking.CookingBatchID);
            var review = InventoryCalculator.ReviewConsumption(cooking.Recipe, cooking.Servings, stock, maps, DateOnly.FromDateTime(DateTime.Today), IncludeExpired);
            Deductions.Clear(); foreach (var line in review.Deductions) Deductions.Add(new(stock.Single(b => b.BatchID == line.BatchID), line.Quantity));
            Shortages.Clear(); foreach (var line in review.Readiness.Ingredients.Where(i => i.Missing > 0 || i.NeedsReview)) Shortages.Add(line);
            NeedsReview = review.Readiness.Status != ReadinessStatus.Ready;
        }
        catch (Exception ex) { AppDiagnostics.Record(ex); ErrorContent = ex is AppDbException db ? db.LocalizedMessage : L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog"); }
        finally { IsBusy = false; }
    }
    [RelayCommand] private async Task Save() {
        ClearError();
        if (cooking.Recipe is null || ProducedPortions is <= 0 or > 1000 || ConsumedPortions < 0 || ConsumedPortions > ProducedPortions || ExpiresOn < DateTime.Today || (NeedsReview && !AcceptIncompleteDeductions) || Deductions.Any(d => d.IsSelected && (d.Quantity <= 0 || d.Quantity > d.Batch.RemainingQuantity))) {
            ErrorContent = L.Message("UX.InvalidCookingReview"); return;
        }
        IsBusy = true;
        try {
            if (_batch is null) {
                var planned = await batches.PlanAsync(session.CurrentUserId, cooking.PlanOperation, cooking.Recipe.RecipeID, cooking.Servings, []);
                cooking.CookingBatchID = planned.Id; cooking.Save();
                _batch = (await batches.GetCookingBatchesAsync(session.CurrentUserId)).Single(b => b.CookingBatchID == planned.Id);
            }
            if (_batch.CookedAt.HasValue) { cooking.CompleteRecipe(); SaveSucceeded?.Invoke(this, EventArgs.Empty); return; }
            var completion = new CookingCompletion(_batch.CookingBatchID, _batch.Version, ProducedPortions, ConsumedPortions,
                DateTime.Today, ExpiresOn?.Date, Deductions.Where(d => d.IsSelected).Select(d => new StockDeduction(d.Batch.BatchID,d.Batch.Version,d.Quantity)).ToArray(), IncludeExpired);
            _operation = cooking.OperationFor(completion);
            await batches.FinishAsync(session.CurrentUserId, _operation, completion);
            cooking.CompleteRecipe(); SaveSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { AppDiagnostics.Record(ex); ErrorContent = ex is AppDbException db ? db.LocalizedMessage : L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog"); }
        finally { IsBusy = false; }
    }
}
