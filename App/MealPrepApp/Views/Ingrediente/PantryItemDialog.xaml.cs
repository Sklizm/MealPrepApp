using System.Windows;
using MealPrepApp.ViewModels.Ingrediente;

namespace MealPrepApp.Views.Ingrediente;

public partial class PantryItemDialog : Window
{
    public PantryItemDialog() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PantryItemDialogViewModel vm)
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
