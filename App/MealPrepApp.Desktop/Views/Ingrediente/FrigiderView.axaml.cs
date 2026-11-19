using Avalonia;
using Avalonia.Controls;

namespace MealPrepApp.Desktop.Views.Ingrediente;

public partial class FrigiderView : UserControl
{
    public FrigiderView() { InitializeComponent(); SizeChanged += (_, _) => UpdatePanels(); }
    private void UpdatePanels()
    {
        var shortWindow = Bounds.Height < 620;
        var narrow = Bounds.Width - 64 < 900;
        FridgePanel.IsVisible = !shortWindow;
        CategoryStrip.IsVisible = shortWindow;
        var stacked = shortWindow || narrow;
        Classes.Set("compact", narrow);
        Panels.ColumnDefinitions = new ColumnDefinitions(stacked ? "*" : "360,20,*");
        Panels.RowDefinitions = new RowDefinitions(shortWindow ? "Auto,8,*" : narrow ? "268,16,*" : "*");
        Grid.SetColumn(StockPanel, stacked ? 0 : 2);
        Grid.SetRow(StockPanel, stacked ? 2 : 0);
    }
    private void OpenAssistant(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => MealPrepApp.Desktop.Views.AssistantWindow.Open(this,"suggest");
}
