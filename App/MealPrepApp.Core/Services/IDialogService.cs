namespace MealPrepApp.Services;

/// <summary>
/// Modal user prompts: confirmations for destructive actions, error and info notices,
/// and ViewModel-driven modal dialogs across Windows, macOS, and Linux.
/// </summary>
public interface IDialogService
{
    /// <summary>Asks a yes/no question. Returns true when the user confirms.</summary>
    bool Confirm(string title, string message);

    /// <summary>Shows a blocking error notice (e.g. a friendly SqlException message).</summary>
    void ShowError(string message);

    /// <summary>Shows a blocking informational notice.</summary>
    void ShowInfo(string title, string message);

    /// <summary>
    /// Opens the corresponding platform window modally with <paramref name="viewModel"/> as its DataContext.
    /// Returns true when confirmed/completed successfully.
    /// </summary>
    bool ShowDialog(object viewModel);
}
