using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MealPrepApp.Models;
using MealPrepApp.ViewModels.Retete;

namespace MealPrepApp.Desktop.Views.Retete;

public partial class ReteteListView : UserControl
{
    private void OpenCard(object? sender,PointerReleasedEventArgs e) {
        if(e.InitialPressMouseButton!=MouseButton.Left || sender is not Border {DataContext:RecipeListItem item} || DataContext is not ReteteListViewModel vm)return;
        if(e.Source is Avalonia.Visual source && source.GetVisualAncestors().Prepend(source).OfType<Button>().Any())return;
        _=MealPrepApp.Desktop.Services.UiOperation.RunAsync(()=>vm.OpenRecipeCommand.ExecuteAsync(item));e.Handled=true;
    }
    private void OpenRecipeMenu(object? sender,Avalonia.Interactivity.RoutedEventArgs e) { if(sender is Button {ContextMenu:{} menu} button){menu.PlacementTarget=button;menu.Open(button);} }
    public ReteteListView()
    {
        InitializeComponent();
    }
}
