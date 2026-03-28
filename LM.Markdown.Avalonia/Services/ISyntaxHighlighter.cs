using Avalonia.Controls.Documents;

namespace LM.Markdown.Avalonia.Services;

public interface ISyntaxHighlighter
{
    IEnumerable<Inline> Highlight(string code, string? language);
    bool IsLanguageSupported(string? language);
}
