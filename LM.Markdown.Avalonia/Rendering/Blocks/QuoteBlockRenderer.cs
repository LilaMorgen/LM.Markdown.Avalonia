using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class QuoteBlockRenderer : BlockRenderer<QuoteBlock>
{
    protected override Control RenderBlock(QuoteBlock block, RenderContext context)
    {
        var panel = new StackPanel();

        foreach (var childBlock in block)
        {
            var rendered = context.RenderBlock(childBlock);
            if (rendered is SelectableTextBlock stb)
            {
                stb.Foreground = context.GetBrush("MarkdownBlockquoteForeground");
                // Remove paragraph bottom margin inside blockquote — the blockquote's
                // own Padding provides symmetric vertical spacing.
                stb.Margin = new Thickness(0);
            }
            panel.Children.Add(rendered);
        }

        var border = new Border
        {
            Child = panel,
            Background = context.GetBrush("MarkdownBlockquoteBackground"),
            BorderBrush = context.GetBrush("MarkdownBlockquoteBorderBrush"),
            BorderThickness = context.GetThickness("MarkdownBlockquoteBorderThickness", new Thickness(3, 0, 0, 0)),
            CornerRadius = context.GetCornerRadius("MarkdownBlockquoteCornerRadius", new CornerRadius(0, 4, 4, 0)),
            Padding = context.GetThickness("MarkdownBlockquotePadding", new Thickness(16, 8, 16, 8)),
            Margin = context.GetThickness("MarkdownBlockMargin", new Thickness(0, 0, 0, 12)),
        };

        return border;
    }
}
