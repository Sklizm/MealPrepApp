using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MealPrepApp.ViewModels.Rapoarte;

namespace MealPrepApp.Views.Rapoarte;

public partial class ListaCumparaturiPrintView : UserControl
{
    public ListaCumparaturiPrintView() => InitializeComponent();

    private void OnPrintClick(object sender, RoutedEventArgs e)
    {
        // Button is IsEnabled-bound to Rows.Count, so the empty case can't happen here.
        if (DataContext is not ListaCumparaturiPrintViewModel vm || vm.Rows.Count == 0)
            return;

        var print = new PrintDialog();
        if (print.ShowDialog() != true) return;

        var doc = BuildPrintDocument(vm);
        doc.PageHeight = print.PrintableAreaHeight;
        doc.PageWidth = print.PrintableAreaWidth;
        doc.PagePadding = new Thickness(48);
        doc.ColumnGap = 0;
        doc.ColumnWidth = print.PrintableAreaWidth;

        print.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Lista de cumparaturi");
    }

    private static FlowDocument BuildPrintDocument(ListaCumparaturiPrintViewModel vm)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
        };

        doc.Blocks.Add(new Paragraph(new Run("Lista de cumparaturi"))
        {
            FontSize = 18,
            FontWeight = FontWeights.Bold,
        });
        doc.Blocks.Add(new Paragraph(new Run(
            $"Interval: {vm.StartDate:dd.MM.yyyy} – {vm.EndDate:dd.MM.yyyy}")));

        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var header = new TableRowGroup();
        header.Rows.Add(MakeRow(bold: true, "Ingredient", "Necesar", "In frigider", "De cumparat", "Unitate"));
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var r in vm.Rows)
        {
            body.Rows.Add(MakeRow(
                bold: false,
                r.IngredientName,
                r.NeededQty.ToString("0.##"),
                r.OnHandQty.ToString("0.##"),
                r.ToBuyQty.ToString("0.##"),
                r.UnitAbbreviation));
        }
        table.RowGroups.Add(body);

        doc.Blocks.Add(table);
        return doc;
    }

    private static TableRow MakeRow(bool bold, params string[] cells)
    {
        var row = new TableRow();
        foreach (var text in cells)
        {
            var para = new Paragraph(new Run(text));
            if (bold) para.FontWeight = FontWeights.Bold;
            row.Cells.Add(new TableCell(para)
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 0.5),
                Padding = new Thickness(4),
            });
        }
        return row;
    }
}
