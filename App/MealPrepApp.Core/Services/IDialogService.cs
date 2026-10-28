namespace MealPrepApp.Services;

/// <summary>Awaitable platform prompts. Closing a dialog without confirmation returns false.</summary>
public interface IDialogService
{
    async Task<LeaveDecision> ChooseLeaveAsync()
    {
        if (await ConfirmAsync("Modificări nesalvate", "Salvezi modificările într-o ciornă înainte de a pleca?")) return LeaveDecision.SaveDraft;
        return await ConfirmAsync("Renunță la modificări", "Ștergi modificările nesalvate?") ? LeaveDecision.Discard : LeaveDecision.Stay;
    }
    Task<bool> ConfirmAsync(string title, string message);
    Task ShowErrorAsync(string message);
    Task ShowInfoAsync(string title, string message);
    Task<bool> ShowDialogAsync(object viewModel);
}
