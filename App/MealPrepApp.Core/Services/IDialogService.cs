namespace MealPrepApp.Services;

/// <summary>Awaitable platform prompts. Closing a dialog without confirmation returns false.</summary>
public interface IDialogService
{
    async Task<LeaveDecision> ChooseLeaveAsync()
    {
        if (await ConfirmAsync(L.Message("Text.UnsavedChanges"), L.Message("Text.SaveChangesAsADraftBeforeLeaving"))) return LeaveDecision.SaveDraft;
        return await ConfirmAsync(L.Message("Text.DiscardChanges"), L.Message("Text.DiscardUnsavedChanges")) ? LeaveDecision.Discard : LeaveDecision.Stay;
    }
    Task<bool> ConfirmAsync(LocalizedMessage title, LocalizedMessage message) => ConfirmAsync(title.ToString(), message.ToString());
    Task ShowErrorAsync(LocalizedMessage message) => ShowErrorAsync(message.ToString());
    Task ShowInfoAsync(LocalizedMessage title, LocalizedMessage message) => ShowInfoAsync(title.ToString(), message.ToString());
    Task<bool> ConfirmAsync(string title, string message);
    Task ShowErrorAsync(string message);
    Task ShowInfoAsync(string title, string message);
    Task<bool> ShowDialogAsync(object viewModel);
}
