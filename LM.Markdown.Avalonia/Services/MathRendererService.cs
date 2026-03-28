using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaMath.Controls;

namespace LM.Markdown.Avalonia.Services;

public class MathRendererService : IMathRenderer
{
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

        return normalized.Replace("\r\n", "\n").Replace("\r", "\n");
    }
}
