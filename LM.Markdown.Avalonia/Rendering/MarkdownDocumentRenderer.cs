using Avalonia.Controls;
using Avalonia.Media;
using LM.Markdown.Avalonia.Rendering.Blocks;
using LM.Markdown.Avalonia.Rendering.Inlines;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Rendering;

public class MarkdownDocumentRenderer
{
    private readonly RenderContext _context;

    public RenderContext Context => _context;

    public MarkdownDocumentRenderer(RenderContext context)
    {
        _context = context;
        RegisterDefaultRenderers();
    }

    private void RegisterDefaultRenderers()
    {
        // MathBlockRenderer must be before FencedCodeBlockRenderer because MathBlock extends FencedCodeBlock
        _context.RegisterBlockRenderer(new MathBlockRenderer());
        _context.RegisterBlockRenderer(new MermaidBlockRenderer());
        _context.RegisterBlockRenderer(new FencedCodeBlockRenderer());
        _context.RegisterBlockRenderer(new CodeBlockRenderer());
        _context.RegisterBlockRenderer(new HeadingBlockRenderer());
        _context.RegisterBlockRenderer(new ImageParagraphBlockRenderer());
        _context.RegisterBlockRenderer(new ParagraphBlockRenderer());
        _context.RegisterBlockRenderer(new QuoteBlockRenderer());
        _context.RegisterBlockRenderer(new ListBlockRenderer());
        _context.RegisterBlockRenderer(new TableBlockRenderer());
        _context.RegisterBlockRenderer(new ThematicBreakRenderer());
        _context.RegisterBlockRenderer(new HtmlBlockRenderer());

        _context.RegisterInlineRenderer(new LiteralInlineRenderer());
        _context.RegisterInlineRenderer(new EmphasisInlineRenderer());
        _context.RegisterInlineRenderer(new CodeInlineRenderer());
        _context.RegisterInlineRenderer(new LinkInlineRenderer());
        _context.RegisterInlineRenderer(new LineBreakInlineRenderer());
        _context.RegisterInlineRenderer(new MathInlineRenderer());
        _context.RegisterInlineRenderer(new HtmlEntityInlineRenderer());
        _context.RegisterInlineRenderer(new AutolinkInlineRenderer());
        _context.RegisterInlineRenderer(new HtmlInlineRenderer());
    }

    public IReadOnlyList<Control> Render(MarkdownDocument document)
    {
        var controls = new List<Control>();

        foreach (var block in document)
        {
            if (ShouldSkipBlock(block))
                continue;

            try
            {
                var control = _context.RenderBlock(block);
                controls.Add(control);
            }
            catch
            {
                controls.Add(new TextBlock
                {
                    Text = block.ToString(),
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.5,
                });
            }
        }

        return controls;
    }

    public (int firstChanged, IReadOnlyList<Control> newControls) RenderIncremental(
        MarkdownDocument oldDoc,
        string oldMarkdown,
        MarkdownDocument newDoc,
        string newMarkdown)
    {
        var firstChanged = 0;
        var oldIndex = 0;
        var newIndex = 0;

        while (oldIndex < oldDoc.Count && newIndex < newDoc.Count)
        {
            var oldBlock = oldDoc[oldIndex];
            var newBlock = newDoc[newIndex];

            if (ShouldSkipBlock(oldBlock))
            {
                oldIndex++;
                continue;
            }

            if (ShouldSkipBlock(newBlock))
            {
                newIndex++;
                continue;
            }

            if (!BlocksEqual(oldBlock, oldMarkdown, newBlock, newMarkdown))
                break;

            firstChanged++;
            oldIndex++;
            newIndex++;
        }

        var newControls = new List<Control>();
        for (var i = newIndex; i < newDoc.Count; i++)
        {
            var block = newDoc[i];
            if (ShouldSkipBlock(block))
                continue;

            try
            {
                newControls.Add(_context.RenderBlock(block));
            }
            catch
            {
                newControls.Add(new TextBlock
                {
                    Text = block.ToString(),
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.5,
                });
            }
        }

        return (firstChanged, newControls);
    }

    private static bool ShouldSkipBlock(Block block)
        => block.GetType().Name == "LinkReferenceDefinitionGroup";

    private static bool BlocksEqual(Block a, string sourceA, Block b, string sourceB)
    {
        if (a.GetType() != b.GetType())
            return false;

        if (a is FencedCodeBlock fencedA && b is FencedCodeBlock fencedB)
        {
            return string.Equals(fencedA.Info, fencedB.Info, StringComparison.Ordinal)
                && string.Equals(fencedA.Arguments, fencedB.Arguments, StringComparison.Ordinal)
                && string.Equals(fencedA.Lines.ToString(), fencedB.Lines.ToString(), StringComparison.Ordinal);
        }

        return GetBlockSource(sourceA, a).SequenceEqual(GetBlockSource(sourceB, b));
    }

    private static ReadOnlySpan<char> GetBlockSource(string markdown, Block block)
    {
        if (string.IsNullOrEmpty(markdown))
            return ReadOnlySpan<char>.Empty;

        var start = Math.Clamp(block.Span.Start, 0, markdown.Length);
        var end = Math.Clamp(block.Span.End, -1, markdown.Length - 1);
        if (end < start)
            return ReadOnlySpan<char>.Empty;

        return markdown.AsSpan(start, end - start + 1);
    }
}
