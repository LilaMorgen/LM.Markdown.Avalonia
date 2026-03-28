using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig.Helpers;
using System.Linq;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace LM.Avalonia.Markdown;

public sealed class TextMateCodeHighlighter : CodeHighlighter
{
    private static readonly FontFamily MonospaceFontFamily = new("Cascadia Mono,Consolas,Courier New");
    private static readonly IBrush DefaultForeground = Brushes.WhiteSmoke;
    private static readonly IReadOnlyDictionary<string, string> ScopeAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["c#"] = "source.cs",
            ["cs"] = "source.cs",
            ["csharp"] = "source.cs",
            ["fs"] = "source.fsharp",
            ["fsharp"] = "source.fsharp",
            ["vb"] = "source.vbnet",
            ["javascript"] = "source.js",
            ["js"] = "source.js",
            ["typescript"] = "source.ts",
            ["ts"] = "source.ts",
            ["json"] = "source.json",
            ["html"] = "text.html.basic",
            ["xml"] = "text.xml",
            ["xaml"] = "text.xml",
            ["css"] = "source.css",
            ["scss"] = "source.css.scss",
            ["sql"] = "source.sql",
            ["md"] = "text.html.markdown",
            ["markdown"] = "text.html.markdown",
            ["yml"] = "source.yaml",
            ["yaml"] = "source.yaml",
            ["ps1"] = "source.powershell",
            ["powershell"] = "source.powershell",
            ["sh"] = "source.shell",
            ["bash"] = "source.shell"
        };

    private readonly Dictionary<string, IGrammar?> _grammarCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IBrush> _brushCache = new(StringComparer.OrdinalIgnoreCase);
    private RegistryOptions _options;
    private Registry _registry;
    private TextMateSharp.Themes.Theme _theme;
    private ThemeName _themeName;
    private IGrammar? _currentGrammar;
    private IStateStack? _ruleStack;

    public TextMateCodeHighlighter()
        : this(ThemeName.DarkPlus)
    {
    }

    public TextMateCodeHighlighter(ThemeName themeName)
    {
        _themeName = themeName;
        _options = new RegistryOptions(themeName);
        _registry = new Registry(_options);
        _theme = _registry.GetTheme();
    }

    public ThemeName ThemeName
    {
        get => _themeName;
        set
        {
            if (_themeName == value)
            {
                return;
            }

            _themeName = value;
            _options = new RegistryOptions(value);
            _registry = new Registry(_options);
            _theme = _registry.GetTheme();
            _grammarCache.Clear();
            _brushCache.Clear();
            _currentGrammar = null;
            _ruleStack = null;
            OnInvalidated();
        }
    }

    internal override void BeginBlock(string? language)
    {
        _currentGrammar = ResolveGrammar(language);
        _ruleStack = null;
    }

    internal override void EndBlock()
    {
        _currentGrammar = null;
        _ruleStack = null;
    }

    internal override CodeHighlightResult? Highlight(StringLine line, string? language)
    {
        return HighlightText(line.ToString(), language);
    }

    internal CodeHighlightResult? HighlightText(string text, string? language)
    {
        _currentGrammar ??= ResolveGrammar(language);
        if (_currentGrammar is null)
        {
            return null;
        }

        var result = _currentGrammar.TokenizeLine(text, _ruleStack, TimeSpan.MaxValue);
        _ruleStack = result.RuleStack;

        return new ControlCodeHighlightResult(BuildTextBlock(text, result.Tokens));
    }

    private SelectableTextBlock BuildTextBlock(string text, IReadOnlyList<IToken> tokens)
    {
        var textBlock = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = MonospaceFontFamily,
            Foreground = DefaultForeground
        };

        if (tokens.Count == 0)
        {
            textBlock.Text = text;
            return textBlock;
        }

        textBlock.Inlines ??= new InlineCollection();

        var nextStart = 0;
        foreach (var token in tokens)
        {
            var start = Math.Clamp(token.StartIndex, 0, text.Length);
            var end = Math.Clamp(token.EndIndex, start, text.Length);

            if (start > nextStart)
            {
                textBlock.Inlines.Add(new Run(text[nextStart..start]));
            }

            if (end > start)
            {
                var run = new Run(text[start..end]);
                ApplyTokenStyle(run, token.Scopes);
                textBlock.Inlines.Add(run);
            }

            nextStart = end;
        }

        if (nextStart < text.Length)
        {
            textBlock.Inlines.Add(new Run(text[nextStart..]));
        }

        if (textBlock.Inlines.Count == 0)
        {
            textBlock.Text = text;
        }

        return textBlock;
    }

    private void ApplyTokenStyle(Run run, IReadOnlyList<string> scopes)
    {
        var themeRules = _theme.Match(scopes.ToList());
        if (themeRules.Count == 0)
        {
            run.Foreground = DefaultForeground;
            return;
        }

        var rule = themeRules[0];
        var foreground = _theme.GetColor(rule.foreground);
        run.Foreground = string.IsNullOrWhiteSpace(foreground) ? DefaultForeground : GetBrush(foreground);
    }

    private IBrush GetBrush(string color)
    {
        if (_brushCache.TryGetValue(color, out var brush))
        {
            return brush;
        }

        brush = new SolidColorBrush(Color.Parse(color));
        _brushCache[color] = brush;
        return brush;
    }

    private IGrammar? ResolveGrammar(string? language)
    {
        var scopeName = ResolveScopeName(language);
        if (scopeName is null)
        {
            return null;
        }

        if (_grammarCache.TryGetValue(scopeName, out var cached))
        {
            return cached;
        }

        var grammar = _registry.LoadGrammar(scopeName);
        _grammarCache[scopeName] = grammar;
        return grammar;
    }

    private string? ResolveScopeName(string? language)
    {
        var normalizedLanguage = NormalizeLanguage(language);
        if (normalizedLanguage is null)
        {
            return null;
        }

        if (ScopeAliases.TryGetValue(normalizedLanguage, out var scopeAlias))
        {
            return scopeAlias;
        }

        if (normalizedLanguage.StartsWith("source.", StringComparison.OrdinalIgnoreCase) ||
            normalizedLanguage.StartsWith("text.", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedLanguage;
        }

        try
        {
            return _options.GetScopeByLanguageId(normalizedLanguage);
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var normalized = language.Trim();
        var separatorIndex = normalized.IndexOfAny([' ', '\t', ',', ';', ':', '{']);
        if (separatorIndex >= 0)
        {
            normalized = normalized[..separatorIndex];
        }

        return normalized.Trim().ToLowerInvariant();
    }
}