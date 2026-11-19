using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.ComponentModel;
using Avalonia.VisualTree;
using MealPrepApp.Desktop.Services;
using MealPrepApp.Models;
using MealPrepApp.ViewModels.Retete;

namespace MealPrepApp.Desktop.Views.Retete;

public partial class ReteteListView : UserControl
{
    private ReteteListViewModel? _list;
    private (string Filter, string Search, int Category, string Sort, int Page) _query;
    private bool _restoring;

    public ReteteListView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => AttachList();
        DetachedFromVisualTree += (_, _) => DetachList();
        DataContextChanged += (_, _) => { if (this.IsAttachedToVisualTree()) AttachList(); };
        RecipeScroll.ScrollChanged += (_, _) => RememberOffset(RecipeScroll);
        DraftScroll.ScrollChanged += (_, _) => RememberOffset(DraftScroll);
    }

    private void AttachList()
    {
        DetachList();
        if (DataContext is not ReteteListViewModel list) return;
        _list = list;
        _query = list.ListQuery;
        list.PropertyChanged += ListChanged;
        RestorePosition();
    }

    private void DetachList()
    {
        if (_list is not null) _list.PropertyChanged -= ListChanged;
        _list = null;
    }

    private void ListChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_list is not { } list) return;
        var next = list.ListQuery;
        if (next != _query)
        {
            _query = next;
            list.ListOffset = 0;
            list.LastOpenedRecipeId = null;
            RecipeScroll.Offset = DraftScroll.Offset = default;
        }
        if (args.PropertyName == nameof(list.IsBusy) && !list.IsBusy) RestorePosition();
    }

    private ScrollViewer ScrollFor(ReteteListViewModel list) => list.IsShowingRecipes ? RecipeScroll : DraftScroll;

    private void RememberOffset(ScrollViewer viewer)
    {
        if (!_restoring && _list is { IsBusy: false } list && ScrollFor(list) == viewer)
            list.ListOffset = viewer.Offset.Y;
    }

    private void RestorePosition()
    {
        if (_list is not { } list) return;
        _restoring = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_list != list) return;
            var viewer = ScrollFor(list);
            viewer.Offset = new Vector(viewer.Offset.X, list.ListOffset);
            if (list.LastOpenedRecipeId is { } recipeId)
            {
                var button = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(control =>
                    control.Name == "RecipeTitleButton" && control.DataContext is RecipeListItem item && item.RecipeID == recipeId);
                if (button is not null)
                {
                    list.LastOpenedRecipeId = null;
                    button.Focus();
                }
            }
            _restoring = false;
        }, DispatcherPriority.Loaded);
    }

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
