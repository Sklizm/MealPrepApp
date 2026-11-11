using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Retete;
namespace MealPrepApp.Desktop.Views.Retete;
public partial class PortionEventDialog:Window
{
    public PortionEventDialog(){InitializeComponent();DataContextChanged+=(_,_)=>{if(DataContext is PortionEventDialogViewModel vm)vm.SaveSucceeded+=(_,_)=>Close(true);};}
    private void Cancel(object? sender,RoutedEventArgs e)=>Close(false);
}
