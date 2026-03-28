using System.Net;
using Avalonia.Controls.Documents;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;

namespace LM.Markdown.Avalonia.Rendering.Inlines;

public class HtmlEntityInlineRenderer : InlineRenderer<HtmlEntityInline>
{
    protected override IEnumerable<AvaloniaInline> RenderInline(HtmlEntityInline inline, RenderContext context)
    {
        var decoded = WebUtility.HtmlDecode(inline.Transcoded.ToString());
        yield return new Run(decoded);
    }
}

public class AutolinkInlineRenderer : InlineRenderer<AutolinkInline>
{
    protected override IEnumerable<AvaloniaInline> RenderInline(AutolinkInline inline, RenderContext context)
    {
        var url = inline.Url;
        if (inline.IsEmail && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            url = "mailto:" + url;

        var linkBrush = context.GetBrush("MarkdownLinkForeground");

        var textBlock = new TextBlock
        {
            Text = inline.Url,
            Foreground = linkBrush,
            Cursor = new Cursor(StandardCursorType.Hand),
            TextDecorations = TextDecorations.Underline,
        };

        textBlock.PointerPressed += (_, _) => context.OnLinkClicked(url);

        yield return new InlineUIContainer { Child = textBlock };
    }
}

public class HtmlInlineRenderer : InlineRenderer<HtmlInline>
{
    protected override IEnumerable<AvaloniaInline> RenderInline(HtmlInline inline, RenderContext context)
    {
        var tag = inline.Tag;

        if (tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase))
        {
            yield return new LineBreak();
            yield break;
        }

        // Render raw HTML as plain text fallback
        yield return new Run(tag)
        {
            Foreground = context.GetBrush("MarkdownSecondaryForeground"),
            FontSize = context.GetDouble("MarkdownSmallFontSize", 12),
        };
    }
}
