using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Ingrediente;

namespace MealPrepApp.Desktop.Views.Ingrediente;

public partial class IngredientAddDialog : Window
{
    public IngredientAddDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is IngredientAddDialogViewModel vm)
                vm.SaveSucceeded += (_, _) =>
                {
                    Tag = true;
                    Close(true);
                };
        };
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Tag = false;
        Close(false);
    }
}
