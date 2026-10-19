using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Planificare;

namespace MealPrepApp.Desktop.Views.Planificare;

public partial class PlanMealDialog : Window
{
    public PlanMealDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is PlanMealDialogViewModel vm)
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
