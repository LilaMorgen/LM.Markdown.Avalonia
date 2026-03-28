using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AvaloniaColor = Avalonia.Media.Color;
using Mermaider;
using Mermaider.Models;

namespace LM.Markdown.Avalonia.Services;

public class MermaidRendererService : IMermaidRenderer
{
    private const string DefaultBgColor = "#FFFFFF";
    private const string DefaultFgColor = "#27272A";
    private const string DefaultAccentColor = "#3B82F6";
    private const int MaxCacheEntries = 128;

    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _cacheOrder = new();
    private string? _bgColor;
    private string? _fgColor;

    public string? BgColor
    {
        get => _bgColor;
        set => SetColor(ref _bgColor, value);
    }

    public string? FgColor
    {
        get => _fgColor;
        set => SetColor(ref _fgColor, value);
    }

    public string? RenderToSvg(string mermaidCode)
    {
        if (string.IsNullOrWhiteSpace(mermaidCode))
            return null;

        var palette = MermaidSvgPalette.Create(BgColor, FgColor);
        var cacheKey = $"{palette.Bg}\n{palette.Fg}\n{palette.Accent}\n{mermaidCode}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var svg = MermaidRenderer.RenderSvg(mermaidCode, new RenderOptions
            {
                Bg = palette.Bg,
                Fg = palette.Fg,
                Accent = palette.Accent,
                Muted = palette.TextSecondary,
                Line = palette.Line,
                Surface = palette.NodeFill,
                Border = palette.NodeStroke,
            });
            svg = NormalizeForAvalonia(svg, palette);
            if (_cache.TryAdd(cacheKey, svg))
            {
                _cacheOrder.Enqueue(cacheKey);
                TrimCache();
            }
            else
            {
                _cache[cacheKey] = svg;
            }

            return svg;
        }
        catch
        {
            return null;
        }
    }

    public void ClearCache()
    {
        while (_cacheOrder.TryDequeue(out _))
        {
        }

        _cache.Clear();
    }

    private void SetColor(ref string? field, string? value)
    {
        if (string.Equals(field, value, StringComparison.OrdinalIgnoreCase))
            return;

        field = value;
        ClearCache();
    }

    private void TrimCache()
    {
        while (_cache.Count > MaxCacheEntries && _cacheOrder.TryDequeue(out var key))
        {
            _cache.TryRemove(key, out _);
        }
    }

    private static string NormalizeForAvalonia(string svg, MermaidSvgPalette palette)
    {
        svg = Regex.Replace(svg, "\\sstyle=\"--[^\"]*\"", string.Empty, RegexOptions.CultureInvariant);
        svg = Regex.Replace(svg, "svg\\s*\\{.*?\\}", string.Empty, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        svg = Regex.Replace(svg, "\\.node\\s*,\\s*\\.actor\\s*,\\s*\\.entity\\s*,\\s*\\.class-node\\s*\\{[^}]*filter:[^}]*\\}", string.Empty, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        svg = Regex.Replace(svg, "\\.subgraph\\s*\\{[^}]*filter:[^}]*\\}", string.Empty, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        svg = Regex.Replace(svg, "<text(?![^>]*font-family=)", "<text font-family=\"Inter, Segoe UI, sans-serif\"", RegexOptions.CultureInvariant);

        foreach (var replacement in palette.GetReplacements())
        {
            svg = svg.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
        }

        svg = Regex.Replace(svg, "<style>\\s*</style>", string.Empty, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        svg = Regex.Replace(svg, "(?:\\r?\\n){3,}", Environment.NewLine + Environment.NewLine, RegexOptions.CultureInvariant);

        return svg;
    }

    private readonly record struct MermaidSvgPalette(
        string Bg,
        string Fg,
        string Accent,
        string TextSecondary,
        string TextMuted,
        string TextFaint,
        string Line,
        string Arrow,
        string NodeFill,
        string NodeStroke,
        string GroupFill,
        string GroupHeader,
        string GroupStroke,
        string InnerStroke,
        string KeyBadge,
        string AccentFill,
        string AccentStroke,
        string AccentText)
    {
        public static MermaidSvgPalette Create(string? bg, string? fg)
        {
            var background = ParseColorOrDefault(bg, DefaultBgColor);
            var foreground = ParseColorOrDefault(fg, DefaultFgColor);
            var accent = ParseColorOrDefault(DefaultAccentColor, DefaultAccentColor);

            return new MermaidSvgPalette(
                ToHex(background),
                ToHex(foreground),
                ToHex(accent),
                ToHex(Mix(foreground, background, 0.55)),
                ToHex(Mix(foreground, background, 0.35)),
                ToHex(Mix(foreground, background, 0.20)),
                ToHex(Mix(foreground, background, 0.32)),
                ToHex(accent),
                ToHex(Mix(foreground, background, 0.04)),
                ToHex(Mix(foreground, background, 0.22)),
                ToHex(background),
                ToHex(Mix(foreground, background, 0.04)),
                ToHex(Mix(foreground, background, 0.10)),
                ToHex(Mix(foreground, background, 0.10)),
                ToHex(Mix(foreground, background, 0.08)),
                ToHex(Mix(accent, background, 0.08)),
                ToHex(Mix(accent, background, 0.20)),
                ToHex(Mix(accent, background, 0.65)));
        }

        public IReadOnlyDictionary<string, string> GetReplacements()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["var(--bg)"] = Bg,
                ["var(--fg)"] = Fg,
                ["var(--accent)"] = Accent,
                ["var(--_text)"] = Fg,
                ["var(--_text-sec)"] = TextSecondary,
                ["var(--_text-muted)"] = TextMuted,
                ["var(--_text-faint)"] = TextFaint,
                ["var(--_line)"] = Line,
                ["var(--_arrow)"] = Arrow,
                ["var(--_node-fill)"] = NodeFill,
                ["var(--_node-stroke)"] = NodeStroke,
                ["var(--_group-fill)"] = GroupFill,
                ["var(--_group-hdr)"] = GroupHeader,
                ["var(--_group-stroke)"] = GroupStroke,
                ["var(--_inner-stroke)"] = InnerStroke,
                ["var(--_key-badge)"] = KeyBadge,
                ["var(--_accent-fill)"] = AccentFill,
                ["var(--_accent-stroke)"] = AccentStroke,
                ["var(--_accent-text)"] = AccentText,
            };
        }

        private static AvaloniaColor ParseColorOrDefault(string? value, string fallback)
        {
            try
            {
                return AvaloniaColor.Parse(string.IsNullOrWhiteSpace(value) ? fallback : value);
            }
            catch
            {
                return AvaloniaColor.Parse(fallback);
            }
        }

        private static AvaloniaColor Mix(AvaloniaColor foreground, AvaloniaColor background, double foregroundWeight)
        {
            var backgroundWeight = 1d - foregroundWeight;

            static byte Blend(byte first, double firstWeight, byte second, double secondWeight)
            {
                return (byte)Math.Clamp(Math.Round((first * firstWeight) + (second * secondWeight)), 0, 255);
            }

            return AvaloniaColor.FromArgb(
                Blend(foreground.A, foregroundWeight, background.A, backgroundWeight),
                Blend(foreground.R, foregroundWeight, background.R, backgroundWeight),
                Blend(foreground.G, foregroundWeight, background.G, backgroundWeight),
                Blend(foreground.B, foregroundWeight, background.B, backgroundWeight));
        }

        private static string ToHex(AvaloniaColor color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
