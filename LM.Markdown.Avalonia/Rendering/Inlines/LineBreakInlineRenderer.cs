using Avalonia.Controls.Documents;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;

namespace LM.Markdown.Avalonia.Rendering.Inlines;

public class LineBreakInlineRenderer : InlineRenderer<LineBreakInline>
{
    protected override IEnumerable<AvaloniaInline> RenderInline(LineBreakInline inline, RenderContext context)
    {
        if (inline.IsHard)
        {
            yield return new LineBreak();
        }
        else
        {
            yield return new Run(" ");
        }
    }
}
