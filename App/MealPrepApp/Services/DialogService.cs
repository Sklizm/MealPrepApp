using System.Linq;
using System.Windows;
using MealPrepApp.Views.Shared;

namespace MealPrepApp.Services;

/// <summary>
/// Routes every confirm / error / info prompt through the styled
/// <see cref="MessageDialog"/> so nothing in the app shows raw <see cref="MessageBox"/>
/// chrome. Modal dialog opening (<see cref="ShowDialog{TWindow}"/>) is unchanged.
/// </summary>
public sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message) =>
        MessageDialog.Show(MessageDialogKind.Confirm, title, message, okText: "Da", cancelText: "Nu");

    public void ShowError(string message) =>
        MessageDialog.Show(MessageDialogKind.Error, "Eroare", message);

    public void ShowInfo(string title, string message) =>
        MessageDialog.Show(MessageDialogKind.Info, title, message);

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
