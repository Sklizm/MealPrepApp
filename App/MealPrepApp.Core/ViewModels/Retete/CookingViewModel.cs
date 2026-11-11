using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Services;
using Microsoft.Extensions.DependencyInjection;
namespace MealPrepApp.ViewModels.Retete;
public sealed partial class CookingViewModel(CookingSession session, IServiceProvider? services = null) : ViewModelBase
{
    public CookingSession Session { get; } = session;
    [RelayCommand] private async Task FinishCooking() {
        if (services?.GetService<CookingBatchRepository>() is not { } batches || Session.Recipe is null) return;
        try {
            var review = new FinishCookingDialogViewModel(Session, batches, services.GetRequiredService<InventoryRepository>(), services.GetRequiredService<ISessionService>());
            await review.LoadAsync();
            if (await services.GetRequiredService<IDialogService>().ShowDialogAsync(review)) NotificationContent = L.Message("UX.CookingRecorded");
        }
        catch (Exception ex) { AppDiagnostics.Record(ex); ErrorContent = L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog"); }
    }
}
