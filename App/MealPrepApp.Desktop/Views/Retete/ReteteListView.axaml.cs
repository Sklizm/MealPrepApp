using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MealPrepApp.Desktop.Services;
using MealPrepApp.Models;
using MealPrepApp.ViewModels.Retete;

namespace MealPrepApp.Desktop.Views.Retete;

public partial class ReteteListView : UserControl
{
    public ReteteListView() => InitializeComponent();

    private void OpenCard(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left ||
            sender is not Border { DataContext: RecipeListItem item } ||
            DataContext is not ReteteListViewModel vm)
            return;
        if (e.Source is Visual source && source.GetVisualAncestors().Prepend(source).OfType<Button>().Any())
            return;

        _ = UiOperation.RunAsync(() => vm.OpenRecipeCommand.ExecuteAsync(item));
        e.Handled = true;
    }

    private void OpenRecipeMenu(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Open(button);
        }
    }

    private void OpenAssistant(object? sender, RoutedEventArgs e) => AssistantWindow.Open(this, "generate");
}
