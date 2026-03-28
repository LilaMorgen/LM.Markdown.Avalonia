using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class HeadingBlockRenderer : BlockRenderer<HeadingBlock>
{
    protected override Control RenderBlock(HeadingBlock block, RenderContext context)
    {
        var fontSize = block.Level switch
        {
            1 => context.GetDouble("MarkdownH1FontSize", 28),
            2 => context.GetDouble("MarkdownH2FontSize", 24),
            3 => context.GetDouble("MarkdownH3FontSize", 20),
            4 => context.GetDouble("MarkdownH4FontSize", 16),
            5 => context.GetDouble("MarkdownH5FontSize", 14),
            _ => context.GetDouble("MarkdownH6FontSize", 13),
        };

        var textBlock = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = context.GetBrush("MarkdownHeadingForeground"),
            FontSize = fontSize,
            FontWeight = FontWeight.Bold,
            FontFamily = context.GetFontFamily("MarkdownFontFamily"),
            LineHeight = fontSize * 1.4,
        };
        context.ApplySelectableTextStyle(textBlock);

        if (block.Inline != null)
        {
            foreach (var inline in context.RenderInlines(block.Inline))
            {
                textBlock.Inlines?.Add(inline);
            }
        }

        // Attach link click/cursor handlers if this heading contains links
        LinkInteractionHelper.AttachIfNeeded(textBlock, context);

        var panel = new StackPanel
        {
            Margin = context.GetThickness("MarkdownHeadingMargin", new Thickness(0, 16, 0, 8)),
        };

        panel.Children.Add(textBlock);

        if (block.Level <= 2)
        {
            var separator = new Border
            {
                Height = 1,
                Background = context.GetBrush("MarkdownHeadingBorderBrush"),
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            panel.Children.Add(separator);
        }

        return panel;
    }
}
