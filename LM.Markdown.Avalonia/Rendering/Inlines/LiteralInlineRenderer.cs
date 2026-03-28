using Avalonia.Controls.Documents;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;

namespace LM.Markdown.Avalonia.Rendering.Inlines;

public class LiteralInlineRenderer : InlineRenderer<LiteralInline>
{
    protected override IEnumerable<AvaloniaInline> RenderInline(LiteralInline inline, RenderContext context)
    {
        if (inline.Content.IsEmpty)
            yield break;

        yield return new Run(inline.Content.ToString());
    }
}
