using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Ingrediente;
namespace MealPrepApp.Desktop.Views.Ingrediente;
public partial class ShoppingItemDialog : Window
{
    public ShoppingItemDialog() {
        InitializeComponent();
        DataContextChanged += (_, _) => { if (DataContext is ShoppingItemDialogViewModel vm) vm.SaveSucceeded += (_, _) => Close(true); };
    }
    private void Cancel(object? sender, RoutedEventArgs e) => Close(false);
}
