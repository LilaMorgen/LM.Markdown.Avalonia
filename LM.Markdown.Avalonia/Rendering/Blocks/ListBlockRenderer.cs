using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class ListBlockRenderer : BlockRenderer<ListBlock>
{
    protected override Control RenderBlock(ListBlock block, RenderContext context)
    {
        var panel = new StackPanel
        {
            Margin = context.GetThickness("MarkdownBlockMargin", new Thickness(0, 0, 0, 12)),
        };

        var index = block.IsOrdered ? (block.OrderedStart?.Length > 0 ? int.TryParse(block.OrderedStart, out var s) ? s : 1 : 1) : 0;

        foreach (var item in block)
        {
            if (item is ListItemBlock listItem)
            {
                var itemPanel = new DockPanel
                {
                    Margin = context.GetThickness("MarkdownListItemMargin", new Thickness(0, 2, 0, 2)),
                };

                // Check for task list items
                var isTaskItem = false;
                var isChecked = false;

                if (listItem.Count > 0 && listItem[0] is ParagraphBlock paragraph && paragraph.Inline?.FirstChild is TaskList taskList)
                {
                    isTaskItem = true;
                    isChecked = taskList.Checked;
                }

                if (isTaskItem)
                {
                    var lineHeight = context.GetDouble("MarkdownFontSize", 14) * context.GetDouble("MarkdownLineHeight", 1.6);

                    var checkBox = new CheckBox
                    {
                        IsChecked = isChecked,
                        IsEnabled = false,
                        // Center within a fixed-height wrapper that matches the text LineHeight
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0),
                    };

                    // Wrapper matches the first text line's height so the checkbox
                    // visually aligns with the text baseline.
                    var checkWrapper = new Border
                    {
                        Child = checkBox,
                        Height = lineHeight,
                        VerticalAlignment = VerticalAlignment.Top,
                    };
                    DockPanel.SetDock(checkWrapper, Dock.Left);
                    itemPanel.Children.Add(checkWrapper);
                }
                else
                {
                    var bulletText = block.IsOrdered
                        ? $"{index}."
                        : "•";

                    var bullet = new TextBlock
                    {
                        Text = bulletText,
                        Foreground = context.GetBrush("MarkdownForeground"),
                        FontSize = context.GetDouble("MarkdownFontSize", 14),
                        FontFamily = context.GetFontFamily("MarkdownFontFamily"),
                        // Match the paragraph's LineHeight so the bullet baseline
                        // aligns with the content text baseline.
                        LineHeight = context.GetDouble("MarkdownFontSize", 14) * context.GetDouble("MarkdownLineHeight", 1.6),
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 0, 8, 0),
                        MinWidth = block.IsOrdered ? 20 : 12,
                    };
                    DockPanel.SetDock(bullet, Dock.Left);
                    itemPanel.Children.Add(bullet);
                }

                var contentPanel = new StackPanel();
                foreach (var childBlock in listItem)
                {
                    if (isTaskItem && childBlock is ParagraphBlock p && p.Inline?.FirstChild is TaskList)
                    {
                        // Render paragraph without the task list checkbox marker
                        var tb = new SelectableTextBlock
                        {
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = context.GetBrush("MarkdownForeground"),
                            FontSize = context.GetDouble("MarkdownFontSize", 14),
                            FontFamily = context.GetFontFamily("MarkdownFontFamily"),
                            LineHeight = context.GetDouble("MarkdownFontSize", 14) * context.GetDouble("MarkdownLineHeight", 1.6),
                        };

                        if (p.Inline != null)
                        {
                            var child = p.Inline.FirstChild?.NextSibling; // Skip the TaskList inline
                            while (child != null)
                            {
                                foreach (var inline in context.RenderInline(child))
                                    tb.Inlines?.Add(inline);
                                child = child.NextSibling;
                            }
                        }

                        // Attach link click/cursor handlers if needed
                        LinkInteractionHelper.AttachIfNeeded(tb, context);

                        contentPanel.Children.Add(tb);
                    }
                    else
                    {
                        var rendered = context.RenderBlock(childBlock);
                        // Sub-lists inside list items inherit the outer list's block margin
                        // which creates excessive blank space. Remove it.
                        if (childBlock is ListBlock && rendered is StackPanel sp)
                        {
                            sp.Margin = new Thickness(sp.Margin.Left, sp.Margin.Top, sp.Margin.Right, 0);
                        }
                        contentPanel.Children.Add(rendered);
                    }
                }

                itemPanel.Children.Add(contentPanel);

                var indentWrapper = new Border
                {
                    Child = itemPanel,
                    Padding = context.GetThickness("MarkdownListIndent", new Thickness(24, 0, 0, 0)),
                };

                panel.Children.Add(indentWrapper);
                index++;
            }
        }

        return panel;
    }
}
