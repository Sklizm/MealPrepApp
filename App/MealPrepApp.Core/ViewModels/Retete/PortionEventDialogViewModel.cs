using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
namespace MealPrepApp.ViewModels.Retete;
public sealed partial class PortionEventDialogViewModel(CookingBatch batch,CookingBatchRepository repository,ISessionService session,bool discard) : ViewModelBase
{
    public CookingBatch Batch {get;}=batch;
    public bool IsDiscard {get;}=discard;
    [LocalizedProperty] public string ActionLabel=>L.Text(IsDiscard?"UX.DiscardPrepared":"UX.ConsumeLeftovers");
    [ObservableProperty] private decimal _quantity=Math.Min(1,batch.FreePortions);
    private Guid _operation=Guid.NewGuid();private decimal? _submitted;
    public event EventHandler? SaveSucceeded;
    [RelayCommand] private async Task Save() {
        if(Quantity<=0||Quantity>Batch.FreePortions){ErrorContent=L.Message("UX.InvalidCookingReview");return;}
        if(_submitted!=Quantity){_operation=Guid.NewGuid();_submitted=Quantity;}
        IsBusy=true;
        try{await repository.PortionEventAsync(session.CurrentUserId,_operation,Batch.CookingBatchID,Batch.Version,Quantity,IsDiscard?"Discarded":"Consumed");SaveSucceeded?.Invoke(this,EventArgs.Empty);}
        catch(Exception ex){AppDiagnostics.Record(ex);ErrorContent=ex is AppDbException db?db.LocalizedMessage:L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog");}
        finally{IsBusy=false;}
    }
}
