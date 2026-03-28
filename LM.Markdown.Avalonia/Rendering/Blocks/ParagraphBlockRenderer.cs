using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class ParagraphBlockRenderer : BlockRenderer<ParagraphBlock>
{
    protected override Control RenderBlock(ParagraphBlock block, RenderContext context)
    {
        var textBlock = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = context.GetBrush("MarkdownForeground"),
            FontSize = context.GetDouble("MarkdownFontSize", 14),
            FontFamily = context.GetFontFamily("MarkdownFontFamily"),
            Margin = context.GetThickness("MarkdownParagraphMargin", new Thickness(0, 0, 0, 10)),
            LineHeight = context.GetDouble("MarkdownFontSize", 14) * context.GetDouble("MarkdownLineHeight", 1.6),
        };

        if (block.Inline != null)
        {
            foreach (var inline in context.RenderInlines(block.Inline))
            {
                textBlock.Inlines?.Add(inline);
            }
        }

        // Attach link click/cursor handlers if this paragraph contains links
        LinkInteractionHelper.AttachIfNeeded(textBlock, context);

        return textBlock;
    }
}
