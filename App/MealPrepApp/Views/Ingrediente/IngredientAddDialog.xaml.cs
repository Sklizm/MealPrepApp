using System.Windows;
using MealPrepApp.ViewModels.Ingrediente;

namespace MealPrepApp.Views.Ingrediente;

public partial class IngredientAddDialog : Window
{
    public IngredientAddDialog() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IngredientAddDialogViewModel vm)
            vm.SaveSucceeded += OnSaveSucceeded;
    }

    private void OnSaveSucceeded(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnChromeCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
