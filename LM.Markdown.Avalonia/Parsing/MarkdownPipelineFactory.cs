using Markdig;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Parsing;

public static class MarkdownPipelineFactory
{
    public static MarkdownPipeline Create()
    {
        return new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseMathematics()
            .UseTaskLists()
            .UsePipeTables()
            .UseGridTables()
            .UseAutoLinks()
            .UseEmphasisExtras()
            .UseGenericAttributes()
            .UseSoftlineBreakAsHardlineBreak()
            .Build();
    }

    public static MarkdownDocument Parse(string markdown, MarkdownPipeline? pipeline = null)
    {
        pipeline ??= Create();
        return Markdig.Markdown.Parse(markdown, pipeline);
    }
}
