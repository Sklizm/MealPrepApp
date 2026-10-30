using MealPrepApp.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Ingrediente;
namespace MealPrepApp.Desktop.Views.Ingrediente;
public partial class ShoppingListView:UserControl
{
    public ShoppingListView(){InitializeComponent();}
    private async void CopyText(object? sender,RoutedEventArgs e)
    {
        if(DataContext is not ShoppingListViewModel vm) return;
        try {var clipboard=TopLevel.GetTopLevel(this)?.Clipboard;if(clipboard!=null) {await clipboard.SetTextAsync(vm.PlainText);vm.NotificationContent = L.Message("Text.ListCopied");}}
        catch(Exception ex){MealPrepApp.Services.AppDiagnostics.Record(ex);vm.ErrorContent = L.Message("Text.TheListCouldNotBeCopied");}
    }
}
