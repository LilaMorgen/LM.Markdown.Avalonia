namespace LM.Markdown.Avalonia.Services;

public interface IMermaidRenderer
{
    string? RenderToSvg(string mermaidCode);
}
