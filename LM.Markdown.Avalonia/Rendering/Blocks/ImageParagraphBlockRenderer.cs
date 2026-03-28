using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LM.Markdown.Avalonia.Controls;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class ImageParagraphBlockRenderer : BlockRenderer<ParagraphBlock>
{
    public override bool CanRender(Markdig.Syntax.Block block)
    {
        if (block is not ParagraphBlock paragraph || paragraph.Inline == null)
            return false;

        return TryGetImageSequence(paragraph.Inline, out _);
    }

    protected override Control RenderBlock(ParagraphBlock block, RenderContext context)
    {
        if (block.Inline == null || !TryGetImageSequence(block.Inline, out var images) || images.Count == 0)
        {
            return new TextBlock
            {
                Text = block.ToString(),
                TextWrapping = TextWrapping.Wrap,
            };
        }

        var panel = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        foreach (var imageInline in images)
        {
            var imageControl = MarkdownImageControl.Create(imageInline, context, isBlockImage: true);
            imageControl.HorizontalAlignment = HorizontalAlignment.Stretch;
            panel.Children.Add(imageControl);
        }

        var overlay = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
        };
        overlay.Classes.Add(CrossBlockSelectionHandler.SelectableBlockOverlayClass);

        var container = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = false,
        };
        container.Children.Add(panel);
        container.Children.Add(overlay);

        var border = new Border
        {
            Child = container,
            Margin = context.GetThickness("MarkdownBlockMargin", new Thickness(0, 0, 0, 12)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = false,
        };
        border.Classes.Add(CrossBlockSelectionHandler.SelectableBlockClass);

        return border;
    }

    private static bool TryGetImageSequence(ContainerInline container, out List<LinkInline> images)
    {
        images = [];
        var child = container.FirstChild;
        while (child != null)
        {
            switch (child)
            {
                case LiteralInline literal when string.IsNullOrWhiteSpace(literal.Content.ToString()):
                    child = child.NextSibling;
                    continue;
                case LineBreakInline:
                    child = child.NextSibling;
                    continue;
                case LinkInline link when link.IsImage:
                    images.Add(link);
                    child = child.NextSibling;
                    continue;
                default:
                    return false;
            }
        }

        return images.Count > 0;
    }
}