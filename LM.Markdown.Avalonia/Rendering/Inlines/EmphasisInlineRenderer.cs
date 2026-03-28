using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;

namespace LM.Markdown.Avalonia.Rendering.Inlines;

public class EmphasisInlineRenderer : InlineRenderer<EmphasisInline>
{
    protected override IEnumerable<AvaloniaInline> RenderInline(EmphasisInline inline, RenderContext context)
    {
        var children = context.RenderInlines(inline).ToList();

        if (inline.DelimiterChar is '*' or '_')
        {
            if (inline.DelimiterCount == 2)
            {
                var bold = new Bold();
                foreach (var c in children) bold.Inlines.Add(c);
                yield return bold;
            }
            else if (inline.DelimiterCount == 1)
            {
                var italic = new Italic();
                foreach (var c in children) italic.Inlines.Add(c);
                yield return italic;
            }
            else if (inline.DelimiterCount >= 3)
            {
                var bold = new Bold();
                var italic = new Italic();
                foreach (var c in children) italic.Inlines.Add(c);
                bold.Inlines.Add(italic);
                yield return bold;
            }
            else
            {
                foreach (var child in children)
                    yield return child;
            }
        }
        else if (inline.DelimiterChar == '~')
        {
            if (inline.DelimiterCount == 2)
            {
                var span = new Span();
                span.TextDecorations = TextDecorations.Strikethrough;
                foreach (var c in children) span.Inlines.Add(c);
                yield return span;
            }
            else
            {
                foreach (var child in children)
                    yield return child;
            }
        }
        else
        {
            foreach (var child in children)
                yield return child;
        }
    }
}
