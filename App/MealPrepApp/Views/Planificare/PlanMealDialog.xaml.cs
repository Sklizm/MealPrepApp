using System.Windows;
using MealPrepApp.ViewModels.Planificare;

namespace MealPrepApp.Views.Planificare;

public partial class PlanMealDialog : Window
{
    public PlanMealDialog() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlanMealDialogViewModel vm)
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
