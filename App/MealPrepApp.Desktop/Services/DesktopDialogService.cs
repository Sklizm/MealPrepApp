using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MealPrepApp.Desktop.Views.Auth;
using MealPrepApp.Desktop.Views.Ingrediente;
using MealPrepApp.Desktop.Views.Planificare;
using MealPrepApp.Desktop.Views.Shared;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Auth;
using MealPrepApp.ViewModels.Ingrediente;
using MealPrepApp.ViewModels.Planificare;

namespace MealPrepApp.Desktop.Services;

public sealed class DesktopDialogService : IDialogService
{
    public bool Confirm(string title, string message) =>
        MessageDialog.ShowModal(MessageDialogKind.Confirm, title, message, okText: "Da", cancelText: "Nu");

    public void ShowError(string message) =>
        MessageDialog.ShowModal(MessageDialogKind.Error, "Eroare", message);

    public void ShowInfo(string title, string message) =>
        MessageDialog.ShowModal(MessageDialogKind.Info, title, message);

    public bool ShowDialog(object viewModel)
    {
        Window? window = viewModel switch
        {
            PlanMealDialogViewModel => new PlanMealDialog(),
            PantryItemDialogViewModel => new PantryItemDialog(),
            IngredientAddDialogViewModel => new IngredientAddDialog(),
            IngredientNutritionDialogViewModel => new IngredientNutritionDialog(),
            ChangePasswordViewModel => new ChangePasswordDialog(),
            ForgotPasswordViewModel => new ForgotPasswordDialog(),
            _ => null
        };

        if (window is null)
            throw new NotSupportedException($"No desktop dialog window registered for '{viewModel.GetType().Name}'.");

        window.DataContext = viewModel;
        var owner = ActiveOwner();
        if (owner is not null)
        {
            var frame = new DispatcherFrame();
            bool result = false;

            // Wire result property if the window has one, or close
            window.Closed += (_, _) => frame.Continue = false;
            window.Show(owner);
            Dispatcher.UIThread.PushFrame(frame);

            // In our modal dialogs, tag or a property indicates confirmation
            if (window.Tag is bool b) result = b;
            else result = true;

            return result;
        }

        return false;
    }

    private static Window? ActiveOwner()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
        }
        return null;
    }
}
