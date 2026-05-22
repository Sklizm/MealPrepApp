using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MealPrepApp.ViewModels.Rapoarte;

namespace MealPrepApp.Views.Rapoarte;

public partial class PlanSaptamanalPrintView : UserControl
{
    public PlanSaptamanalPrintView() => InitializeComponent();

    private void OnPrintClick(object sender, RoutedEventArgs e)
    {
        // Button is IsEnabled-bound to Days.Count, so the empty case can't happen here.
        if (DataContext is not PlanSaptamanalPrintViewModel vm || vm.Days.Count == 0)
            return;

        var print = new PrintDialog();
        if (print.ShowDialog() != true) return;

        var doc = BuildPrintDocument(vm);
        doc.PageHeight = print.PrintableAreaHeight;
        doc.PageWidth = print.PrintableAreaWidth;
        doc.PagePadding = new Thickness(48);
        doc.ColumnGap = 0;
        doc.ColumnWidth = print.PrintableAreaWidth;

        print.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Plan saptamanal");
    }

    private static FlowDocument BuildPrintDocument(PlanSaptamanalPrintViewModel vm)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
        };

        doc.Blocks.Add(new Paragraph(new Run("Plan saptamanal"))
        {
            FontSize = 18,
            FontWeight = FontWeights.Bold,
        });
        doc.Blocks.Add(new Paragraph(new Run(vm.WeekLabel)));

        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
        for (int i = 0; i < 4; i++)
            table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });

        var header = new TableRowGroup();
        header.Rows.Add(MakeRow(bold: true, "Zi", "Mic dejun", "Pranz", "Cina", "Gustare"));
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var d in vm.Days)
            body.Rows.Add(MakeRow(bold: false, d.Day, d.Breakfast, d.Lunch, d.Dinner, d.Snack));
        table.RowGroups.Add(body);

        doc.Blocks.Add(table);
        return doc;
    }

    private static TableRow MakeRow(bool bold, params string[] cells)
    {
        var row = new TableRow();
        foreach (var text in cells)
        {
            var para = new Paragraph();
            // Cells may hold several titles, one per line — render each on its own line.
            var lines = (text ?? "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) para.Inlines.Add(new LineBreak());
                para.Inlines.Add(new Run(lines[i].TrimEnd('\r')));
            }
            if (bold) para.FontWeight = FontWeights.Bold;
            row.Cells.Add(new TableCell(para)
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(4),
            });
        }
        return row;
    }
}
