using AvaloniaInline = Avalonia.Controls.Documents.Inline;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace LM.Markdown.Avalonia.Rendering;

public interface IInlineRenderer
{
    bool CanRender(MarkdigInline inline);
    IEnumerable<AvaloniaInline> Render(MarkdigInline inline, RenderContext context);
}

public abstract class InlineRenderer<TInline> : IInlineRenderer where TInline : MarkdigInline
{
    public bool CanRender(MarkdigInline inline) => inline is TInline;

    public IEnumerable<AvaloniaInline> Render(MarkdigInline inline, RenderContext context)
        => RenderInline((TInline)inline, context);

    protected abstract IEnumerable<AvaloniaInline> RenderInline(TInline inline, RenderContext context);
}
