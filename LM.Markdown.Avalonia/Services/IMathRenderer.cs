using Avalonia.Controls;
using Avalonia.Media;

namespace LM.Markdown.Avalonia.Services;

/// <summary>
/// Creates native Avalonia controls that render LaTeX math.
/// </summary>
public interface IMathRenderer
{
    /// <summary>
    /// Creates a control that renders the given LaTeX formula.
    /// </summary>
    Control? CreateFormulaControl(string latex, double scale, IBrush? foreground);
}
