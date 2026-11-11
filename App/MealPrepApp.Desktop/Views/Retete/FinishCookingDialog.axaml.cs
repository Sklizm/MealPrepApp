using Avalonia.Controls;
using Avalonia.Interactivity;
using MealPrepApp.ViewModels.Retete;
namespace MealPrepApp.Desktop.Views.Retete;
public partial class FinishCookingDialog : Window
{
    public FinishCookingDialog() { InitializeComponent(); DataContextChanged += (_, _) => { if (DataContext is FinishCookingDialogViewModel vm) vm.SaveSucceeded += (_, _) => Close(true); }; }
    private void Cancel(object? sender, RoutedEventArgs e) => Close(false);
}
