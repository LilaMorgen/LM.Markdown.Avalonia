using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Markdig.Extensions.Mathematics;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;

namespace LM.Markdown.Avalonia.Rendering.Inlines;

public class MathInlineRenderer : InlineRenderer<MathInline>
{
    protected override IEnumerable<AvaloniaInline> RenderInline(MathInline inline, RenderContext context)
    {
        var latex = inline.Content.ToString().Trim();

        if (context.MathRenderer != null)
        {
            try
            {
                var foreground = context.GetBrush("MarkdownForeground");
                var scale = context.GetDouble("MarkdownFontSize", 14);

                var formulaControl = context.MathRenderer.CreateFormulaControl(latex, scale, foreground);
                if (formulaControl != null)
                {
                    var verticalNudge = Math.Clamp(scale * 0.04, 0.5, 1);

                    // InlineUIContainer aligns relative to its own box. Because the
                    // selection overlay reserves a line-height-sized box, centering the
                    // formula inside that box lifts it above the surrounding text.
                    // Bottom-align the formula and use baseline alignment for the
                    // container so the formula sits on the same line baseline.
                    formulaControl.VerticalAlignment = VerticalAlignment.Bottom;
                    formulaControl.Margin = new Thickness(0, verticalNudge, 0, 0);

                    // Use a Grid so we can separate the selection highlight
                    // (height-constrained to the text line-height) from the
                    // formula itself (rendered at its natural height on top).
                    //
                    // Children Z-order (back → front):
                    //   [0] selectionBg  – line-height-tall Border whose
                    //                     Background is toggled by
                    //                     CrossBlockSelectionHandler
                    //   [1] formulaControl – full-height formula on top
                    //
                    // IsHitTestVisible = false lets pointer events pass through
                    // to the parent SelectableTextBlock.
                    var lineHeight = context.GetDouble("MarkdownFontSize", 14)
                                   * context.GetDouble("MarkdownLineHeight", 1.6);

                    var selectionBg = new Border
                    {
                        Height = lineHeight,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false,
                    };

                    var wrapper = new Grid { IsHitTestVisible = false };
                    wrapper.Children.Add(selectionBg);      // behind
                    wrapper.Children.Add(formulaControl);   // on top

                    return [new InlineUIContainer(wrapper) { BaselineAlignment = global::Avalonia.Media.BaselineAlignment.Baseline }];
                }
            }
            catch
            {
                // Fall through to fallback
            }
        }

        // Fallback: show LaTeX source styled
        var span = new Span
        {
            Background = context.GetBrush("MarkdownInlineCodeBackground"),
            Foreground = context.GetBrush("MarkdownInlineCodeForeground"),
            FontFamily = context.GetFontFamily("MarkdownCodeFontFamily"),
            FontSize = context.GetDouble("MarkdownCodeFontSize", 13),
            FontStyle = FontStyle.Italic,
        };
        span.Inlines.Add(new Run($"${latex}$"));
        return [span];
    }
}
