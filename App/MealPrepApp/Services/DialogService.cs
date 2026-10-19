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
    public bool Confirm(string title, string message) =>
        MessageDialog.Show(MessageDialogKind.Confirm, title, message, okText: "Da", cancelText: "Nu");

    public void ShowError(string message) =>
        MessageDialog.Show(MessageDialogKind.Error, "Eroare", message);

    public void ShowInfo(string title, string message) =>
        MessageDialog.Show(MessageDialogKind.Info, title, message);

    public bool ShowDialog(object viewModel) => viewModel switch
    {
        PlanMealDialogViewModel => ShowDialog<PlanMealDialog>(viewModel),
        PantryItemDialogViewModel => ShowDialog<PantryItemDialog>(viewModel),
        IngredientAddDialogViewModel => ShowDialog<IngredientAddDialog>(viewModel),
        IngredientNutritionDialogViewModel => ShowDialog<IngredientNutritionDialog>(viewModel),
        ChangePasswordViewModel => ShowDialog<ChangePasswordDialog>(viewModel),
        ForgotPasswordViewModel => ShowDialog<ForgotPasswordDialog>(viewModel),
        _ => throw new NotSupportedException($"No WPF dialog registered for ViewModel '{viewModel.GetType().Name}'.")
    };

    public bool ShowDialog<TWindow>(object viewModel) where TWindow : Window, new()
    {
        var window = new TWindow
        {
            DataContext = viewModel,
            Owner = ActiveOwner(),
        };
        return window.ShowDialog() == true;
    }

    private static Window? ActiveOwner()
    {
        var active = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive);
        return active ?? Application.Current?.MainWindow;
    }
}
