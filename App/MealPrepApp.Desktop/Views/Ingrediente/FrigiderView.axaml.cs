using Avalonia;
using Avalonia.Controls;

namespace MealPrepApp.Desktop.Views.Ingrediente;

public partial class FrigiderView : UserControl
{
    public FrigiderView() { InitializeComponent(); SizeChanged += (_, _) => UpdatePanels(); }
    private void UpdatePanels()
    {
        var narrow = Bounds.Width - 48 < 900;
        Classes.Set("compact", narrow);
        Panels.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "360,20,*");
        Panels.RowDefinitions = new RowDefinitions(narrow ? "244,12,*" : "*");
        Grid.SetColumn(StockPanel, narrow ? 0 : 2);
        Grid.SetRow(StockPanel, narrow ? 2 : 0);
    }
}
