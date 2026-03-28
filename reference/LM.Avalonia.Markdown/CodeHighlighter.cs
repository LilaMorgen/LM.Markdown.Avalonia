using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig.Helpers;

namespace LM.Avalonia.Markdown;

public abstract class CodeHighlighter
{
    internal event EventHandler? Invalidated;

    internal virtual void BeginBlock(string? language)
    {
    }

    internal virtual void EndBlock()
    {
    }

    protected internal void OnInvalidated()
    {
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    internal abstract CodeHighlightResult? Highlight(StringLine line, string? language);
}

internal abstract class CodeHighlightResult
{
    public abstract Control BuildControl();
}

internal sealed class InlineCodeHighlightResult(Inline inline) : CodeHighlightResult
{
    public Inline Inline { get; } = inline;

    public override Control BuildControl()
    {
        var textBlock = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Cascadia Mono,Consolas,Courier New")
        };
        textBlock.Inlines ??= new InlineCollection();
        textBlock.Inlines.Add(Inline);
        return textBlock;
    }
}

internal sealed class ControlCodeHighlightResult(Control control) : CodeHighlightResult
{
    public Control Control { get; } = control;

    public override Control BuildControl() => Control;
}