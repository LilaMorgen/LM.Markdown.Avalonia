using Markdig;
using Markdig.Extensions.AutoLinks;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Tables;
using Markdig.Helpers;
using Markdig.Renderers;

namespace LM.Avalonia.Markdown;

public static class MarkdownExtensions
{
    public static MarkdownPipelineBuilder UseSupportedExtensions(this MarkdownPipelineBuilder pipeline)
    {
        return pipeline
            .UseAutoLinks((AutoLinkOptions?)null)
            .UseAlertBlocks((Action<HtmlRenderer, StringSlice>?)null)
            .UseEmojiAndSmiley(true)
            .UseFootnotes()
            .UseGridTables()
            .UsePipeTables((PipeTableOptions?)null)
            .UseEmphasisExtras((EmphasisExtraOptions)31)
            .Use<MarkdownSymbolsExtension>();
    }
}

internal sealed class MarkdownSymbolsExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.InlineParsers.Contains<MarkdownSymbolsInlineParser>())
        {
            pipeline.InlineParsers.Insert(0, new MarkdownSymbolsInlineParser());
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}

internal sealed class MarkdownSymbolsInlineParser : Markdig.Parsers.InlineParser
{
    public MarkdownSymbolsInlineParser()
    {
        OpeningCharacters = ['(', '+'];
    }

    public override bool Match(Markdig.Parsers.InlineProcessor processor, ref StringSlice slice)
    {
        switch (slice.CurrentChar)
        {
            case '(':
                var c1 = slice.PeekChar(1);
                var c2 = slice.PeekChar(2);
                var c3 = slice.PeekChar(3);
                if (char.ToLowerInvariant(c1) == 'c' && c2 == ')')
                {
                    processor.Inline = new Markdig.Syntax.Inlines.LiteralInline("©");
                    slice.Start += 3;
                    return true;
                }

                if (char.ToLowerInvariant(c1) == 'r' && c2 == ')')
                {
                    processor.Inline = new Markdig.Syntax.Inlines.LiteralInline("®");
                    slice.Start += 3;
                    return true;
                }

                if (char.ToLowerInvariant(c1) == 't' && char.ToLowerInvariant(c2) == 'm' && c3 == ')')
                {
                    processor.Inline = new Markdig.Syntax.Inlines.LiteralInline("™");
                    slice.Start += 4;
                    return true;
                }

                break;
            case '+':
                if (slice.PeekChar(1) == '-')
                {
                    processor.Inline = new Markdig.Syntax.Inlines.LiteralInline("±");
                    slice.Start += 2;
                    return true;
                }

                break;
        }

        return false;
    }
}