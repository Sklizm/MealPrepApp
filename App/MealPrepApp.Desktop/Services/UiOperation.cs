using MealPrepApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Desktop.Services;

internal static class UiOperation
{
    public static async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            await App.Services.GetRequiredService<IDialogService>()
                .ShowErrorAsync(L.Text("Text.TheOperationCouldNotBeCompletedCheckTheAppLog"));
        }
    }
}
