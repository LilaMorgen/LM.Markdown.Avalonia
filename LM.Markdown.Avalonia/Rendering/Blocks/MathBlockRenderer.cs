using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class MathBlockRenderer : BlockRenderer<MathBlock>
{
    protected override Control RenderBlock(MathBlock block, RenderContext context)
    {
        var latex = block.Lines.ToString().Trim();

        if (context.MathRenderer != null)
        {
            try
            {
                var foreground = context.GetBrush("MarkdownForeground");
                var scale = context.GetDouble("MarkdownFontSize", 14) * 1.5;

                var formulaControl = context.MathRenderer.CreateFormulaControl(latex, scale, foreground);
                if (formulaControl != null)
                {
                    formulaControl.HorizontalAlignment = HorizontalAlignment.Center;

                    var border = new Border
                    {
                        Child = formulaControl,
                        Padding = context.GetThickness("MarkdownSpecialBlockPadding", new Thickness(16)),
                        Margin = context.GetThickness("MarkdownBlockMargin", new Thickness(0, 0, 0, 12)),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                    };

                    // Mark so CrossBlockSelectionHandler can include this non-text
                    // block in multi-block selection and show a selection overlay.
                    border.Classes.Add("selectable-block");

                    return border;
                }
            }
            catch
            {
                // Fall through to fallback
            }
        }

        // Fallback: show LaTeX source
        var textBlock = new SelectableTextBlock
        {
            Text = $"$${latex}$$",
            FontFamily = context.GetFontFamily("MarkdownCodeFontFamily"),
            FontSize = context.GetDouble("MarkdownCodeFontSize", 13),
            Foreground = context.GetBrush("MarkdownCodeBlockForeground"),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontStyle = FontStyle.Italic,
        };

        var fallbackBorder = new Border
        {
            Child = textBlock,
            Background = context.GetBrush("MarkdownSpecialBlockBackground"),
            BorderBrush = context.GetBrush("MarkdownSpecialBlockBorderBrush"),
            BorderThickness = context.GetThickness("MarkdownSpecialBlockBorderThickness", new Thickness(1)),
            CornerRadius = context.GetCornerRadius("MarkdownSpecialBlockCornerRadius", new CornerRadius(8)),
            Padding = context.GetThickness("MarkdownSpecialBlockPadding", new Thickness(16)),
            Margin = context.GetThickness("MarkdownBlockMargin", new Thickness(0, 0, 0, 12)),
        };

        return fallbackBorder;
    }
}
