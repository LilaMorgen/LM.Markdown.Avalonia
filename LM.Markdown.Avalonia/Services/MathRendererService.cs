using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaMath.Controls;
using System.Text.RegularExpressions;

namespace LM.Markdown.Avalonia.Services;

public class MathRendererService : IMathRenderer
{
    private static readonly Regex BMatrixEnvironmentRegex = new(
        @"\\(?<keyword>begin|end)\{bmatrix\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BMatrixCommandRegex = new(
        @"\\bmatrix(?=\s*\{)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Control? CreateFormulaControl(string latex, double scale, IBrush? foreground)
    {
        latex = NormalizeLatex(latex);
        if (string.IsNullOrWhiteSpace(latex))
            return null;

        var formulaBlock = new FormulaBlock
        {
            Formula = latex,
            Scale = scale,
        };

        if (foreground != null)
        {
            formulaBlock.Foreground = foreground;
        }

        return formulaBlock;
    }

    private static string NormalizeLatex(string latex)
    {
        if (string.IsNullOrWhiteSpace(latex))
            return string.Empty;

        var normalized = latex.Trim();
        if (normalized.StartsWith("$$") && normalized.EndsWith("$$") && normalized.Length > 4)
        {
            normalized = normalized[2..^2].Trim();
        }
        else if (normalized.StartsWith('$') && normalized.EndsWith('$') && normalized.Length > 2)
        {
            normalized = normalized[1..^1].Trim();
        }

        normalized = normalized.Replace("\r\n", "\n").Replace("\r", "\n");

        // XAML-Math supports pmatrix but not bmatrix. Map the common LaTeX
        // square-bracket matrix syntax to the equivalent command it understands.
        normalized = BMatrixEnvironmentRegex.Replace(normalized, static match =>
            $"\\{match.Groups["keyword"].Value}{{pmatrix}}");
        normalized = BMatrixCommandRegex.Replace(normalized, @"\pmatrix");

        return normalized;
    }
}
