using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;

namespace LM.Markdown.Avalonia.Rendering.Inlines;

public class CodeInlineRenderer : InlineRenderer<CodeInline>
{
    protected override IEnumerable<AvaloniaInline> RenderInline(CodeInline inline, RenderContext context)
    {
        var span = new Span
        {
            Background = context.GetBrush("MarkdownInlineCodeBackground"),
            Foreground = context.GetBrush("MarkdownInlineCodeForeground"),
            FontFamily = context.GetFontFamily("MarkdownCodeFontFamily"),
            FontSize = context.GetDouble("MarkdownCodeFontSize", 13),
        };
        span.Inlines.Add(new Run(inline.Content));
        yield return span;
    }
}
