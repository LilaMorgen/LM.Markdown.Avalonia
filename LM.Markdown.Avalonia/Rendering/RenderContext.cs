using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LM.Markdown.Avalonia.Events;
using LM.Markdown.Avalonia.Services;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace LM.Markdown.Avalonia.Rendering;

public class RenderContext
{
    private readonly List<IBlockRenderer> _blockRenderers = [];
    private readonly List<IInlineRenderer> _inlineRenderers = [];
    private readonly Dictionary<Control, (int Start, int End)> _sourceSpanMap = new();

    public ISyntaxHighlighter? SyntaxHighlighter { get; set; }
    public IResourceLoader? ResourceLoader { get; set; }
    public IMathRenderer? MathRenderer { get; set; }
    public IMermaidRenderer? MermaidRenderer { get; set; }

    public event EventHandler<LinkClickedEventArgs>? LinkClicked;
    public event EventHandler<ImageClickedEventArgs>? ImageClicked;
    public event EventHandler<CodeBlockCopyEventArgs>? CodeCopied;

    public StyledElement Owner { get; }

    /// <summary>
    /// Maps each rendered Control back to its Markdig Block source character range
    /// (Start inclusive, End inclusive) in the original markdown string.
    /// </summary>
    public IReadOnlyDictionary<Control, (int Start, int End)> SourceSpanMap => _sourceSpanMap;

    public RenderContext(StyledElement owner)
    {
        Owner = owner;
    }

    public void RegisterBlockRenderer(IBlockRenderer renderer) => _blockRenderers.Add(renderer);
    public void RegisterInlineRenderer(IInlineRenderer renderer) => _inlineRenderers.Add(renderer);

    public void ClearSourceSpanMap() => _sourceSpanMap.Clear();

    public void SetSourceSpan(Control control, int start, int end)
        => _sourceSpanMap[control] = (start, end);

    public Control RenderBlock(Markdig.Syntax.Block block)
    {
        foreach (var renderer in _blockRenderers)
        {
            if (renderer.CanRender(block))
            {
                var control = renderer.Render(block, this);
                _sourceSpanMap[control] = (block.Span.Start, block.Span.End);
                return control;
            }
        }

        var fallback = CreateFallbackBlock(block);
        _sourceSpanMap[fallback] = (block.Span.Start, block.Span.End);
        return fallback;
    }

    public IEnumerable<AvaloniaInline> RenderInline(MarkdigInline inline)
    {
        foreach (var renderer in _inlineRenderers)
        {
            if (renderer.CanRender(inline))
                return renderer.Render(inline, this);
        }

        return [new global::Avalonia.Controls.Documents.Run(inline.ToString())];
    }

    public IEnumerable<AvaloniaInline> RenderInlines(ContainerInline container)
    {
        var child = container.FirstChild;
        while (child != null)
        {
            foreach (var inline in RenderInline(child))
            {
                yield return inline;
            }
            child = child.NextSibling;
        }
    }

    public void OnLinkClicked(string url, string? title = null)
        => LinkClicked?.Invoke(Owner, new LinkClickedEventArgs(url, title));

    public void OnImageClicked(string source, string? altText = null)
        => ImageClicked?.Invoke(Owner, new ImageClickedEventArgs(source, altText));

    public void OnCodeCopied(string code, string? language = null)
        => CodeCopied?.Invoke(Owner, new CodeBlockCopyEventArgs(code, language));

    public T GetResource<T>(string key, T fallback)
    {
        if (Owner.TryFindResource(key, Owner.ActualThemeVariant, out var value) && value is T typed)
            return typed;
        return fallback;
    }

    public IBrush GetBrush(string key, IBrush? fallback = null)
        => GetResource(key, fallback ?? Brushes.Transparent);

    public double GetDouble(string key, double fallback = 0)
        => GetResource(key, fallback);

    public Thickness GetThickness(string key, Thickness fallback = default)
        => GetResource(key, fallback);

    public CornerRadius GetCornerRadius(string key, CornerRadius fallback = default)
        => GetResource(key, fallback);

    public FontFamily GetFontFamily(string key, FontFamily? fallback = null)
        => GetResource(key, fallback ?? FontFamily.Default);

    private static Control CreateFallbackBlock(Markdig.Syntax.Block block)
    {
        return new TextBlock
        {
            Text = block.ToString(),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.5,
        };
    }
}
