using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.Tables;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class TableBlockRenderer : BlockRenderer<Table>
{
    protected override Control RenderBlock(Table table, RenderContext context)
    {
        var columnCount = 0;
        foreach (var row in table)
        {
            if (row is TableRow tr && tr.Count > columnCount)
                columnCount = tr.Count;
        }

        if (columnCount == 0)
            return new TextBlock { Text = "[Empty table]" };

        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        for (int c = 0; c < columnCount; c++)
        {
            var width = GridLength.Star;
            if (c < table.ColumnDefinitions.Count && table.ColumnDefinitions[c].Width > 0)
            {
                width = new GridLength(table.ColumnDefinitions[c].Width, GridUnitType.Star);
            }

            grid.ColumnDefinitions.Add(new ColumnDefinition(width));
        }

        int rowIndex = 0;
        foreach (var row in table)
        {
            if (row is not TableRow tableRow)
                continue;

            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var columnIndex = 0;
            for (int c = 0; c < tableRow.Count && columnIndex < columnCount; c++)
            {
                if (tableRow[c] is not TableCell cell)
                    continue;

                var isHeader = tableRow.IsHeader;

                var cellPanel = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                foreach (var cellBlock in cell)
                {
                    var rendered = context.RenderBlock(cellBlock);
                    // Remove paragraph bottom margin inside table cells —
                    // the cell's own Padding provides spacing.
                    if (rendered is SelectableTextBlock stb)
                    {
                        stb.Margin = new Thickness(0);
                        stb.HorizontalAlignment = HorizontalAlignment.Stretch;
                    }
                    cellPanel.Children.Add(rendered);
                }

                // Apply text alignment from column definition
                var alignment = HorizontalAlignment.Left;
                if (columnIndex < table.ColumnDefinitions.Count)
                {
                    alignment = table.ColumnDefinitions[columnIndex].Alignment switch
                    {
                        TableColumnAlign.Center => HorizontalAlignment.Center,
                        TableColumnAlign.Right => HorizontalAlignment.Right,
                        _ => HorizontalAlignment.Left,
                    };
                }

                cellPanel.HorizontalAlignment = alignment == HorizontalAlignment.Left
                    ? HorizontalAlignment.Stretch
                    : alignment;

                var cellBorder = new Border
                {
                    Child = cellPanel,
                    Padding = context.GetThickness("MarkdownTableCellPadding", new Thickness(12, 8)),
                    BorderBrush = context.GetBrush("MarkdownTableBorderBrush"),
                    BorderThickness = new Thickness(
                        columnIndex == 0 ? 0 : 1,
                        rowIndex == 0 ? 0 : 1,
                        0,
                        0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = isHeader
                        ? context.GetBrush("MarkdownTableHeaderBackground")
                        : (rowIndex % 2 == 0
                            ? context.GetBrush("MarkdownTableRowBackground")
                            : context.GetBrush("MarkdownTableRowAlternateBackground")),
                };

                if (isHeader)
                {
                    foreach (var child in cellPanel.Children)
                    {
                        if (child is SelectableTextBlock stb)
                        {
                            stb.FontWeight = FontWeight.Bold;
                            stb.Foreground = context.GetBrush("MarkdownTableHeaderForeground");
                        }
                    }
                }

                Grid.SetRow(cellBorder, rowIndex);
                Grid.SetColumn(cellBorder, columnIndex);

                if (cell.ColumnSpan > 1)
                    Grid.SetColumnSpan(cellBorder, cell.ColumnSpan);

                if (cell.RowSpan > 1)
                    Grid.SetRowSpan(cellBorder, cell.RowSpan);

                grid.Children.Add(cellBorder);
                columnIndex += Math.Max(cell.ColumnSpan, 1);
            }

            rowIndex++;
        }

        var border = new Border
        {
            Child = grid,
            Margin = context.GetThickness("MarkdownBlockMargin", new Thickness(0, 0, 0, 12)),
            BorderBrush = context.GetBrush("MarkdownTableBorderBrush"),
            BorderThickness = context.GetThickness("MarkdownTableBorderThickness", new Thickness(1)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = true,
        };

        return new ScrollViewer
        {
            Content = border,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }
}
