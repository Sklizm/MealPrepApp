using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
namespace MealPrepApp.ViewModels.Ingrediente;
public sealed partial class InventoryBatchDialogViewModel(InventoryBatch batch,InventoryRepository inventory,
    ISessionService session,NotificationCenter notifications,Func<Task>? refresh=null) : ViewModelBase
{
    public InventoryBatch Batch { get; } = batch;
    [ObservableProperty] private decimal _delta;
    [ObservableProperty] private bool _isDiscard;
    private Guid _operation=Guid.NewGuid();
    private (decimal Delta,bool Discard)? _submitted;
    public event EventHandler? SaveSucceeded;
    [RelayCommand] private async Task Save() {
        ClearError(); if(Delta==0 || (IsDiscard&&Delta>0) || Batch.RemainingQuantity+Delta<0) { ErrorContent=L.Message("UX.InvalidStockAdjustment");return; }
        if(_submitted!=(Delta,IsDiscard)) { _operation=Guid.NewGuid();_submitted=(Delta,IsDiscard); }
        IsBusy=true;
        try {
            var result=await inventory.AdjustAsync(session.CurrentUserId,_operation,Batch.BatchID,Batch.Version,Delta,IsDiscard?"Discard":"Correction");
            if(!IsDiscard&&result.EventId is int eventId) notifications.Post(L.Message("UX.StockAdjusted"),async()=>{ await inventory.UndoAsync(session.CurrentUserId,Guid.NewGuid(),eventId); if(refresh is not null)await refresh(); });
            else notifications.Post(L.Message("UX.StockAdjusted"));
            SaveSucceeded?.Invoke(this,EventArgs.Empty);
        } catch(Exception ex) { AppDiagnostics.Record(ex);ErrorContent=ex is AppDbException db?db.LocalizedMessage:L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog"); }
        finally { IsBusy=false; }
    }
}
