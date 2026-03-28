using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class HtmlBlockRenderer : BlockRenderer<HtmlBlock>
{
    protected override Control RenderBlock(HtmlBlock block, RenderContext context)
    {
        var html = block.Lines.ToString().Trim();

        // Display raw HTML as styled code block
        var textBlock = new SelectableTextBlock
        {
            Text = html,
            FontFamily = context.GetFontFamily("MarkdownCodeFontFamily"),
            FontSize = context.GetDouble("MarkdownSmallFontSize", 12),
            Foreground = context.GetBrush("MarkdownSecondaryForeground"),
            TextWrapping = TextWrapping.Wrap,
            Margin = context.GetThickness("MarkdownBlockMargin", new Thickness(0, 0, 0, 12)),
        };

        return textBlock;
    }
}
