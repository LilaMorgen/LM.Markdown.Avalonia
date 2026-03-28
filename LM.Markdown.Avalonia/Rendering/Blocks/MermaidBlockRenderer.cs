using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using LM.Markdown.Avalonia.Controls;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class MermaidBlockRenderer : IBlockRenderer
{
    public bool CanRender(Markdig.Syntax.Block block)
    {
        return block is FencedCodeBlock fenced &&
               string.Equals(GetInfoToken(fenced.Info), "mermaid", StringComparison.OrdinalIgnoreCase);
    }

    public Control Render(Markdig.Syntax.Block block, RenderContext context)
    {
        var fenced = (FencedCodeBlock)block;
        var mermaidCode = fenced.Lines.ToString().Trim();

        if (context.MermaidRenderer != null)
        {
            try
            {
                var svgContent = context.MermaidRenderer.RenderToSvg(mermaidCode);
                if (!string.IsNullOrEmpty(svgContent))
                {
                    var svgSource = TryLoadSvg(svgContent);
                    if (svgSource != null)
                    {
                        var svgImage = new SvgImage { Source = svgSource };
                        var image = new Image
                        {
                            Source = svgImage,
                            Stretch = Stretch.Uniform,
                            MaxWidth = 800,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        };

                        var contentBorder = new Border
                        {
                            Child = image,
                            Background = context.GetBrush("MarkdownSpecialBlockBackground"),
                            BorderBrush = context.GetBrush("MarkdownSpecialBlockBorderBrush"),
                            BorderThickness = context.GetThickness("MarkdownSpecialBlockBorderThickness", new Thickness(1)),
                            CornerRadius = context.GetCornerRadius("MarkdownSpecialBlockCornerRadius", new CornerRadius(8)),
                            Padding = context.GetThickness("MarkdownSpecialBlockPadding", new Thickness(16)),
                        };

                        var overlay = new Border
                        {
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            CornerRadius = context.GetCornerRadius("MarkdownSpecialBlockCornerRadius", new CornerRadius(8)),
                            IsHitTestVisible = false,
                        };
                        overlay.Classes.Add(CrossBlockSelectionHandler.SelectableBlockOverlayClass);

                        var container = new Grid
                        {
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            ClipToBounds = false,
                        };
                        container.Children.Add(contentBorder);
                        container.Children.Add(overlay);

                        var border = new Border
                        {
                            Child = container,
                            Margin = context.GetThickness("MarkdownBlockMargin", new Thickness(0, 0, 0, 12)),
                            CornerRadius = context.GetCornerRadius("MarkdownSpecialBlockCornerRadius", new CornerRadius(8)),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            ClipToBounds = false,
                        };
                        border.Classes.Add(CrossBlockSelectionHandler.SelectableBlockClass);

                        return border;
                    }
                }
            }
            catch
            {
                // Fall through to fallback
            }
        }

        // Fallback: show mermaid source
        var headerLabel = new TextBlock
        {
            Text = "Mermaid (render unavailable)",
            Foreground = context.GetBrush("MarkdownCodeBlockHeaderForeground"),
            FontSize = context.GetDouble("MarkdownSmallFontSize", 12),
            Margin = new Thickness(0, 0, 0, 8),
        };

        var codeBlock = new SelectableTextBlock
        {
            Text = mermaidCode,
            FontFamily = context.GetFontFamily("MarkdownCodeFontFamily"),
            FontSize = context.GetDouble("MarkdownCodeFontSize", 13),
            Foreground = context.GetBrush("MarkdownCodeBlockForeground"),
            TextWrapping = TextWrapping.NoWrap,
        };
        context.ApplySelectableTextStyle(codeBlock);

        var panel = new StackPanel();
        panel.Children.Add(headerLabel);
        panel.Children.Add(codeBlock);

        var fallbackBorder = new Border
        {
            Child = panel,
            Background = context.GetBrush("MarkdownSpecialBlockBackground"),
            BorderBrush = context.GetBrush("MarkdownSpecialBlockBorderBrush"),
            BorderThickness = context.GetThickness("MarkdownSpecialBlockBorderThickness", new Thickness(1)),
            CornerRadius = context.GetCornerRadius("MarkdownSpecialBlockCornerRadius", new CornerRadius(8)),
            Padding = context.GetThickness("MarkdownSpecialBlockPadding", new Thickness(16)),
            Margin = context.GetThickness("MarkdownBlockMargin", new Thickness(0, 0, 0, 12)),
        };

        return fallbackBorder;
    }

    private static SvgSource? TryLoadSvg(string svgContent)
    {
        try
        {
            return SvgSource.LoadFromSvg(svgContent);
        }
        catch
        {
            try
            {
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svgContent));
                return SvgSource.LoadFromStream(stream);
            }
            catch
            {
                return null;
            }
        }
    }

    private static string GetInfoToken(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
            return string.Empty;

        var trimmed = info.Trim();
        var splitIndex = trimmed.IndexOfAny([' ', '\t', '{']);
        return splitIndex > 0 ? trimmed[..splitIndex].Trim() : trimmed;
    }
}
