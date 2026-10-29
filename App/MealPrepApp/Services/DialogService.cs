using System.Linq;
using System.Windows;
using MealPrepApp.ViewModels.Auth;
using MealPrepApp.ViewModels.Ingrediente;
using MealPrepApp.ViewModels.Planificare;
using MealPrepApp.Views.Auth;
using MealPrepApp.Views.Ingrediente;
using MealPrepApp.Views.Planificare;
using MealPrepApp.Views.Shared;

namespace MealPrepApp.Services;

/// <summary>
/// Routes every confirm / error / info prompt through the styled
/// <see cref="MessageDialog"/> and maps ViewModels to their WPF dialog windows.
/// </summary>
public sealed class DialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message) =>
        Task.FromResult(MessageDialog.Show(MessageDialogKind.Confirm, title, message, okText: "Da", cancelText: "Nu"));

    public Task ShowErrorAsync(string message)
    {
        MessageDialog.Show(MessageDialogKind.Error, "Eroare", message);
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string title, string message)
    {
        MessageDialog.Show(MessageDialogKind.Info, title, message);
        return Task.CompletedTask;
    }

    public Task<bool> ShowDialogAsync(object viewModel) => Task.FromResult(viewModel switch
    {
        PlanMealDialogViewModel => ShowDialog(new PlanMealDialog(), viewModel),
        PantryItemDialogViewModel => ShowDialog(new PantryItemDialog(), viewModel),
        IngredientAddDialogViewModel => ShowDialog(new IngredientAddDialog(), viewModel),
        IngredientNutritionDialogViewModel => ShowDialog(new IngredientNutritionDialog(), viewModel),
        ChangePasswordViewModel change => ShowDialog(new ChangePasswordDialog(change), change),
        ForgotPasswordViewModel forgot => ShowDialog(new ForgotPasswordDialog(forgot), forgot),
        _ => throw new NotSupportedException($"No WPF dialog registered for ViewModel '{viewModel.GetType().Name}'.")
    });

    private static bool ShowDialog(Window window, object viewModel)
    {
        window.DataContext = viewModel;
        window.Owner = ActiveOwner();
        try { return window.ShowDialog() == true; }
        finally { if (viewModel is IDisposable disposable) disposable.Dispose(); }
    }

    private static Window? ActiveOwner()
    {
        var active = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive);
        return active ?? Application.Current?.MainWindow;
    }
}
