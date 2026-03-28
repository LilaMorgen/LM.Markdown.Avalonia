using Avalonia.Controls.Documents;
using Avalonia.Media;
using System.Text.Json;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using System.Linq;
using System.Text.RegularExpressions;

namespace LM.Markdown.Avalonia.Services;

public class TextMateSyntaxHighlighter : ISyntaxHighlighter
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> LanguageAliases =
        new(CreateLanguageAliases, isThreadSafe: true);

    private static readonly IReadOnlyDictionary<string, string> SupplementalAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["c#"] = "csharp",
            ["cs"] = "csharp",
            ["csharp"] = "csharp",
            ["f#"] = "fsharp",
            ["fs"] = "fsharp",
            ["vbnet"] = "vb",
            ["js"] = "javascript",
            ["jsx"] = "javascriptreact",
            ["ts"] = "typescript",
            ["tsx"] = "typescriptreact",
            ["md"] = "markdown",
            ["tex"] = "tex",
            ["ps"] = "powershell",
            ["ps1"] = "powershell",
            ["cmd"] = "bat",
            ["shell"] = "shellscript",
            ["sh"] = "shellscript",
            ["bash"] = "shellscript",
            ["zsh"] = "shellscript",
            ["ksh"] = "shellscript",
            ["docker"] = "dockerfile",
            ["containerfile"] = "dockerfile",
            ["make"] = "makefile",
            ["objc"] = "objective-c",
            ["objcpp"] = "objective-cpp",
            ["obj-c"] = "objective-c",
            ["obj-cpp"] = "objective-cpp",
            ["xaml"] = "xml",
            ["axaml"] = "xml",
            ["csproj"] = "xml",
            ["fsproj"] = "xml",
            ["vbproj"] = "xml",
            ["props"] = "xml",
            ["targets"] = "xml",
            ["yml"] = "yaml",
        };

    private readonly RegistryOptions _lightOptions;
    private readonly RegistryOptions _darkOptions;
    private readonly Registry _lightRegistry;
    private readonly Registry _darkRegistry;
    private readonly Dictionary<string, IGrammar?> _lightGrammarCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IGrammar?> _darkGrammarCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IBrush> _brushCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDark;

    public bool IsDark
    {
        get => _isDark;
        set => _isDark = value;
    }

    public TextMateSyntaxHighlighter()
    {
        _lightOptions = new RegistryOptions(ThemeName.LightPlus);
        _darkOptions = new RegistryOptions(ThemeName.DarkPlus);
        _lightRegistry = new Registry(_lightOptions);
        _darkRegistry = new Registry(_darkOptions);
    }

    public bool IsLanguageSupported(string? language)
    {
        try
        {
            return ResolveGrammar(NormalizeLanguage(language)) != null;
        }
        catch
        {
            return false;
        }
    }

    public IEnumerable<Inline> Highlight(string code, string? language)
    {
        language = NormalizeLanguage(language);
        if (string.IsNullOrEmpty(language))
            return [new Run(code)];

        try
        {
            var highlighted = HighlightCore(code, language).ToList();
            if (highlighted.Count > 0)
                return highlighted;

            return HighlightFallback(code, language);
        }
        catch
        {
            return HighlightFallback(code, language);
        }
    }

    private IEnumerable<Inline> HighlightCore(string code, string language)
    {
        var grammar = ResolveGrammar(language);
        if (grammar == null)
            return [new Run(code)];

        var theme = GetRegistry().GetTheme();
        var defaultBrush = GetDefaultForegroundBrush();
        var lines = code.Split('\n');
        var inlines = new List<Inline>();
        IStateStack? ruleStack = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var result = grammar.TokenizeLine(line, ruleStack, TimeSpan.MaxValue);
            ruleStack = result.RuleStack;

            var tokens = result.Tokens;
            if (tokens.Length == 0)
            {
                inlines.Add(new Run(line) { Foreground = defaultBrush });
                if (i < lines.Length - 1)
                    inlines.Add(new LineBreak());
                continue;
            }

            var nextStart = 0;

            foreach (var token in tokens)
            {
                int startIndex = Math.Clamp(token.StartIndex, 0, line.Length);
                int endIndex = Math.Clamp(token.EndIndex, startIndex, line.Length);

                if (startIndex > nextStart)
                {
                    inlines.Add(new Run(line[nextStart..startIndex]) { Foreground = defaultBrush });
                }

                if (endIndex > startIndex)
                {
                    var tokenText = line[startIndex..endIndex];
                    var run = new Run(tokenText)
                    {
                        Foreground = GetTokenColor(token.Scopes, theme) ?? defaultBrush,
                    };
                    inlines.Add(run);
                }

                nextStart = endIndex;
            }

            if (nextStart < line.Length)
            {
                inlines.Add(new Run(line[nextStart..]) { Foreground = defaultBrush });
            }

            if (i < lines.Length - 1)
                inlines.Add(new LineBreak());
        }

        return inlines;
    }

    private static IBrush? GetTokenColor(IList<string> scopes, Theme theme)
    {
        if (scopes == null || scopes.Count == 0)
            return null;

        try
        {
            var settings = theme.Match(scopes.ToList());
            if (settings?.Count > 0)
            {
                var setting = settings[0];
                if (setting.foreground > 0)
                {
                    var hexColor = theme.GetColor(setting.foreground);
                    if (!string.IsNullOrEmpty(hexColor) && Color.TryParse(hexColor, out var color))
                        return new SolidColorBrush(color);
                }
            }
        }
        catch
        {
            // Ignore theme matching errors
        }

        return null;
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return string.Empty;

        var trimmed = language.Trim();
        var splitIndex = trimmed.IndexOfAny([' ', '\t', ',', ';', ':', '{']);
        return splitIndex > 0 ? trimmed[..splitIndex].Trim().ToLowerInvariant() : trimmed.ToLowerInvariant();
    }

    private IGrammar? ResolveGrammar(string? language)
    {
        var scopeName = ResolveScopeName(language);
        if (string.IsNullOrEmpty(scopeName))
            return null;

        var cache = _isDark ? _darkGrammarCache : _lightGrammarCache;
        if (cache.TryGetValue(scopeName, out var cached))
            return cached;

        var grammar = GetRegistry().LoadGrammar(scopeName);
        cache[scopeName] = grammar;
        return grammar;
    }

    private string? ResolveScopeName(string? language)
    {
        var normalizedLanguage = NormalizeLanguage(language);
        if (string.IsNullOrWhiteSpace(normalizedLanguage))
            return null;

        if (normalizedLanguage.StartsWith("source.", StringComparison.OrdinalIgnoreCase) ||
            normalizedLanguage.StartsWith("text.", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedLanguage;
        }

        var canonicalLanguage = ResolveLanguageAlias(normalizedLanguage);

        try
        {
            return GetOptions().GetScopeByLanguageId(canonicalLanguage);
        }
        catch
        {
            return null;
        }
    }

    private RegistryOptions GetOptions() => _isDark ? _darkOptions : _lightOptions;

    private Registry GetRegistry() => _isDark ? _darkRegistry : _lightRegistry;

    private static string ResolveLanguageAlias(string language)
    {
        if (LanguageAliases.Value.TryGetValue(language, out var canonical))
            return canonical;

        var compact = CompactAlias(language);
        if (!string.Equals(compact, language, StringComparison.Ordinal) &&
            LanguageAliases.Value.TryGetValue(compact, out canonical))
        {
            return canonical;
        }

        return language;
    }

    private static IReadOnlyDictionary<string, string> CreateLanguageAliases()
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in SupplementalAliases)
        {
            AddAlias(aliases, pair.Key, pair.Value);
        }

        var assembly = typeof(RegistryOptions).Assembly;
        var resourceNames = assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith("TextMateSharp.Grammars.Resources.Grammars.", StringComparison.Ordinal))
            .Where(name => name.EndsWith(".package.json", StringComparison.Ordinal))
            .Where(name => !name.EndsWith(".package.nls.json", StringComparison.Ordinal));

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                continue;

            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("contributes", out var contributes) ||
                !contributes.TryGetProperty("languages", out var languages) ||
                languages.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var language in languages.EnumerateArray())
            {
                if (!language.TryGetProperty("id", out var idProperty))
                    continue;

                var languageId = idProperty.GetString();
                if (string.IsNullOrWhiteSpace(languageId))
                    continue;

                AddAlias(aliases, languageId, languageId);

                if (language.TryGetProperty("aliases", out var aliasArray) && aliasArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var alias in aliasArray.EnumerateArray())
                    {
                        AddAlias(aliases, alias.GetString(), languageId);
                    }
                }

                if (language.TryGetProperty("extensions", out var extensionArray) && extensionArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var extension in extensionArray.EnumerateArray())
                    {
                        AddExtensionAliases(aliases, extension.GetString(), languageId);
                    }
                }
            }
        }

        return aliases;
    }

    private static void AddExtensionAliases(IDictionary<string, string> aliases, string? extension, string languageId)
    {
        if (string.IsNullOrWhiteSpace(extension) || extension.Contains('*'))
            return;

        var normalized = extension.Trim();
        while (normalized.StartsWith('.'))
        {
            normalized = normalized[1..];
        }

        if (string.IsNullOrWhiteSpace(normalized))
            return;

        AddAlias(aliases, normalized, languageId);
    }

    private static void AddAlias(IDictionary<string, string> aliases, string? alias, string languageId)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return;

        var normalizedAlias = NormalizeLanguage(alias);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
            return;

        aliases[normalizedAlias] = languageId;

        var compactAlias = CompactAlias(normalizedAlias);
        if (!string.IsNullOrWhiteSpace(compactAlias))
        {
            aliases[compactAlias] = languageId;
        }
    }

    private static string CompactAlias(string value)
        => value.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

    private IBrush GetDefaultForegroundBrush()
    {
        var colorKey = _isDark ? "#D4D4D4" : "#1F2328";
        if (_brushCache.TryGetValue(colorKey, out var brush))
            return brush;

        brush = new SolidColorBrush(Color.Parse(colorKey));
        _brushCache[colorKey] = brush;
        return brush;
    }

    private IEnumerable<Inline> HighlightFallback(string code, string language)
    {
        string[] keywords = language switch
        {
            "csharp" or "cs" => ["using", "namespace", "public", "private", "protected", "internal", "class", "struct", "interface", "enum", "return", "var", "new", "async", "await", "if", "else", "for", "foreach", "while", "switch", "case", "null", "true", "false", "void", "string", "int", "bool", "double", "decimal", "Task"],
            "javascript" or "js" or "typescript" or "ts" => ["function", "const", "let", "var", "class", "return", "if", "else", "for", "while", "switch", "case", "null", "true", "false", "new", "import", "export", "async", "await"],
            "python" => ["def", "class", "return", "if", "elif", "else", "for", "while", "in", "import", "from", "as", "None", "True", "False", "async", "await", "try", "except", "with"],
            "json" => ["true", "false", "null"],
            _ => Array.Empty<string>(),
        };

        if (keywords.Length == 0)
            return [new Run(code)];

        var keywordColor = _isDark ? Color.Parse("#5B8FF9") : Color.Parse("#0033AA");
        var stringColor = _isDark ? Color.Parse("#CE9178") : Color.Parse("#A31515");
        var commentColor = _isDark ? Color.Parse("#6A9955") : Color.Parse("#008000");

        var result = new List<Inline>();
        var lines = code.Split('\n');
        var tokenRegex = new Regex("(\"[^\"\\\\]*(?:\\\\.[^\"\\\\]*)*\"|'[^'\\\\]*(?:\\\\.[^'\\\\]*)*'|//.*$|#.*$|\\b\\w+\\b)", RegexOptions.Compiled);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var last = 0;

            foreach (Match match in tokenRegex.Matches(line))
            {
                if (match.Index > last)
                {
                    result.Add(new Run(line[last..match.Index]));
                }

                var text = match.Value;
                var run = new Run(text);

                if (text.StartsWith("//") || text.StartsWith("#"))
                {
                    run.Foreground = new SolidColorBrush(commentColor);
                }
                else if ((text.StartsWith('"') && text.EndsWith('"')) || (text.StartsWith('\'') && text.EndsWith('\'')))
                {
                    run.Foreground = new SolidColorBrush(stringColor);
                }
                else if (Array.IndexOf(keywords, text) >= 0)
                {
                    run.Foreground = new SolidColorBrush(keywordColor);
                    run.FontWeight = FontWeight.Bold;
                }

                result.Add(run);
                last = match.Index + match.Length;
            }

            if (last < line.Length)
            {
                result.Add(new Run(line[last..]));
            }

            if (i < lines.Length - 1)
            {
                result.Add(new LineBreak());
            }
        }

        return result;
    }
}
