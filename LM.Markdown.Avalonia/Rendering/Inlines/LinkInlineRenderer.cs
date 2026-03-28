using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;

namespace LM.Markdown.Avalonia.Rendering.Inlines;

public class LinkInlineRenderer : InlineRenderer<LinkInline>
{
    protected override IEnumerable<AvaloniaInline> RenderInline(LinkInline inline, RenderContext context)
    {
        if (inline.IsImage)
        {
            foreach (var result in RenderImage(inline, context))
                yield return result;
            yield break;
        }

        var url = inline.Url ?? string.Empty;
        var title = inline.Title;

        // Render link as a plain Span — this preserves natural text baseline alignment
        // with surrounding text. Click handling and hand cursor are managed at the
        // parent SelectableTextBlock level by LinkInteractionHelper.
        var span = new Span
        {
            Foreground = context.GetBrush("MarkdownLinkForeground"),
            TextDecorations = TextDecorations.Underline,
        };

        var child = inline.FirstChild;
        while (child != null)
        {
            if (child is LiteralInline literal)
            {
                span.Inlines.Add(new Run(literal.Content.ToString()));
            }
            else
            {
                foreach (var childInline in context.RenderInline(child))
                    span.Inlines.Add(childInline);
            }
            child = child.NextSibling;
        }

        if (span.Inlines.Count == 0)
            span.Inlines.Add(new Run(title ?? url));

        // Mark this Span as a link so LinkInteractionHelper can detect it
        LinkInteractionHelper.MarkAsLink(span, url, title);

        yield return span;
    }

    private static IEnumerable<AvaloniaInline> RenderImage(LinkInline inline, RenderContext context)
    {
        var imageControl = MarkdownImageControl.Create(inline, context, isBlockImage: false);
        yield return new InlineUIContainer { Child = imageControl };
    }
}
