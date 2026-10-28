using Avalonia.Controls;

namespace MealPrepApp.Desktop.Views.Retete;

public partial class ReteteListView : UserControl
{
    private void OpenRecipeMenu(object? sender,Avalonia.Interactivity.RoutedEventArgs e) { if(sender is Button {ContextMenu:{} menu} button){menu.PlacementTarget=button;menu.Open(button);} }
    public ReteteListView()
    {
        InitializeComponent();
    }
}
