using Avalonia.Controls;

namespace MealPrepApp.Desktop.Views.Retete;

public partial class ReteteEditorView : UserControl
{
    public ReteteEditorView()
    {
        InitializeComponent();
    }
    private void OpenAssistant(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => MealPrepApp.Desktop.Views.AssistantWindow.Open(this,"import");
}
