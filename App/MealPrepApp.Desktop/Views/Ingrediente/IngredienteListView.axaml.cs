using Avalonia;
using Avalonia.Controls;

namespace MealPrepApp.Desktop.Views.Ingrediente;

public partial class IngredienteListView : UserControl
{
    public IngredienteListView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateLayoutMode();
    }

    private void UpdateLayoutMode()
    {
        var compact = Bounds.Width < 1000;
        CategoryPanel.IsVisible = !compact;
        CompactCategory.IsVisible = compact;

        // Keep the heading and translated action readable when the shell narrows the view.
        var stackedHeader = Bounds.Width < 780;
        Grid.SetColumn(AddIngredient, stackedHeader ? 0 : 1);
        Grid.SetRow(AddIngredient, stackedHeader ? 1 : 0);
        AddIngredient.HorizontalAlignment = stackedHeader ? Avalonia.Layout.HorizontalAlignment.Left : Avalonia.Layout.HorizontalAlignment.Right;
        AddIngredient.Margin = stackedHeader ? new Thickness(0, 8, 0, 0) : default;

        // Toolbar bounds can lag the view's SizeChanged; use the known panel allocation.
        var stackedToolbar = Bounds.Width - 64 - (compact ? 0 : 280) < 600;
        Grid.SetColumnSpan(CatalogSearch, stackedToolbar ? 3 : 1);
        CatalogSearch.Margin = stackedToolbar ? new Thickness(0, 0, 0, 8) : new Thickness(0, 0, 8, 0);
        Grid.SetColumn(CatalogSort, stackedToolbar ? 0 : 1);
        Grid.SetRow(CatalogSort, stackedToolbar ? 1 : 0);
        Grid.SetColumnSpan(CatalogSort, stackedToolbar ? 2 : 1);
        Grid.SetRow(CatalogRefresh, stackedToolbar ? 1 : 0);
        CatalogToolbar.ColumnDefinitions[1].Width = stackedToolbar ? new GridLength(0) : new GridLength(240);
    }
}
