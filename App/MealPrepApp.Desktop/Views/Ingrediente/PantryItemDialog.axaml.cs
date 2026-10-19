using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Ingrediente;

namespace MealPrepApp.Desktop.Views.Ingrediente;

public partial class PantryItemDialog : Window
{
    public PantryItemDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is PantryItemDialogViewModel vm)
            {
                vm.SaveSucceeded += (_, _) =>
                {
                    Tag = true;
                    Close(true);
                };
            }
        };
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Tag = false;
        Close(false);
    }
}
