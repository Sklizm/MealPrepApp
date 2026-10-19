using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Ingrediente;

namespace MealPrepApp.Desktop.Views.Ingrediente;

public partial class IngredientNutritionDialog : Window
{
    public IngredientNutritionDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is IngredientNutritionDialogViewModel vm)
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
