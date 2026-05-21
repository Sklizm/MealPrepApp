using System.Windows;

namespace MealPrepApp.Services;

/// <summary>
/// Modal user prompts: confirmations for destructive actions, error and info notices.
/// Phase E ships a <see cref="System.Windows.MessageBox"/>-backed implementation; Phase H
/// swaps in the styled <c>ConfirmDialog</c> / <c>ErrorDialog</c> without touching ViewModels.
/// </summary>
public interface IDialogService
{
    /// <summary>Asks a yes/no question. Returns true when the user confirms.</summary>
    bool Confirm(string title, string message);

    /// <summary>Shows a blocking error notice (e.g. a friendly <c>SqlException</c> message).</summary>
    void ShowError(string message);

    /// <summary>Shows a blocking informational notice.</summary>
    void ShowInfo(string title, string message);

    /// <summary>Opens <typeparamref name="TWindow"/> modally over the active window with
    /// <paramref name="viewModel"/> as its DataContext, and returns true when DialogResult is true.
    /// The window must have a parameterless constructor.</summary>
    bool ShowDialog<TWindow>(object viewModel) where TWindow : Window, new();
}
