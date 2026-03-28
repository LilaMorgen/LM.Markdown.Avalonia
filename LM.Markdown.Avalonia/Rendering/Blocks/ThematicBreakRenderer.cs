using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class ThematicBreakRenderer : BlockRenderer<ThematicBreakBlock>
{
    protected override Control RenderBlock(ThematicBreakBlock block, RenderContext context)
    {
        return new Border
        {
            Height = context.GetDouble("MarkdownThematicBreakHeight", 1),
            Background = context.GetBrush("MarkdownThematicBreakBrush"),
            Margin = context.GetThickness("MarkdownThematicBreakMargin", new Thickness(0, 16, 0, 16)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }
}
