using System.Windows;

namespace MealPrepApp.Views;

/// <summary>
/// Small standalone loading window shown after login before the main shell is displayed.
/// </summary>
public partial class StartupLoadingWindow : Window
{
    public StartupLoadingWindow()
    {
        InitializeComponent();
    }
}
