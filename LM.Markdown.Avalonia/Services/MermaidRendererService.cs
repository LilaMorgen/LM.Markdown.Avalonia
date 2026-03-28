using System.Collections.Concurrent;
using Mermaider;

namespace LM.Markdown.Avalonia.Services;

public class MermaidRendererService : IMermaidRenderer
{
    private readonly ConcurrentDictionary<int, string> _cache = new();

    public string? BgColor { get; set; }
    public string? FgColor { get; set; }

    public string? RenderToSvg(string mermaidCode)
    {
        if (string.IsNullOrWhiteSpace(mermaidCode))
            return null;

        var hash = mermaidCode.GetHashCode();
        if (_cache.TryGetValue(hash, out var cached))
            return cached;

        try
        {
            var svg = MermaidRenderer.RenderSvg(mermaidCode);
            _cache.TryAdd(hash, svg);
            return svg;
        }
        catch
        {
            return null;
        }
    }

    public void ClearCache() => _cache.Clear();
}
