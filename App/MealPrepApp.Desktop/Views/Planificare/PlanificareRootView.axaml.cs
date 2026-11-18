using Avalonia.Controls;

namespace MealPrepApp.Desktop.Views.Planificare;

public partial class PlanificareRootView : UserControl
{
    public PlanificareRootView()
    {
        InitializeComponent();
    }
    private void OpenAssistant(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => MealPrepApp.Desktop.Views.AssistantWindow.Open(this,"plan");
}
