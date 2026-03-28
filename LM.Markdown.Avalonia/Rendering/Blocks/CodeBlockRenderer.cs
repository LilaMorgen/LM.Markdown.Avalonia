using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Syntax;
using ScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class CodeBlockRenderer : BlockRenderer<CodeBlock>
{
    public override bool CanRender(Markdig.Syntax.Block block)
        => block is CodeBlock && block is not FencedCodeBlock;

    protected override Control RenderBlock(CodeBlock block, RenderContext context)
    {
        var code = block.Lines.ToString().TrimEnd('\n', '\r');

        var textBlock = new SelectableTextBlock
        {
            Text = code,
            FontFamily = context.GetFontFamily("MarkdownCodeFontFamily"),
            FontSize = context.GetDouble("MarkdownCodeFontSize", 13),
            Foreground = context.GetBrush("MarkdownCodeBlockForeground"),
            TextWrapping = TextWrapping.NoWrap,
        };
        context.ApplySelectableTextStyle(textBlock);

        var border = new Border
        {
            Child = new ScrollViewer
            {
                Content = textBlock,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            },
            Background = context.GetBrush("MarkdownCodeBlockBackground"),
            BorderBrush = context.GetBrush("MarkdownCodeBlockBorderBrush"),
            BorderThickness = context.GetThickness("MarkdownCodeBlockBorderThickness", new Thickness(1)),
            CornerRadius = context.GetCornerRadius("MarkdownCodeBlockCornerRadius", new CornerRadius(8)),
            Padding = context.GetThickness("MarkdownCodeBlockPadding", new Thickness(16)),
            Margin = context.GetThickness("MarkdownBlockMargin", new Thickness(0, 0, 0, 12)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        return border;
    }
}
