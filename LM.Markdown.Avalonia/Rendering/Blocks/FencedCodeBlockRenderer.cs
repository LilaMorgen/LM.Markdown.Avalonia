using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using ShapePath = global::Avalonia.Controls.Shapes.Path;
using Markdig.Syntax;
using ScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility;

namespace LM.Markdown.Avalonia.Rendering.Blocks;

public class FencedCodeBlockRenderer : BlockRenderer<FencedCodeBlock>
{
    protected override Control RenderBlock(FencedCodeBlock block, RenderContext context)
    {
        var language = NormalizeInfoToken(block.Info);
        var code = ExtractCode(block);

        var container = new Border
        {
            Background = context.GetBrush("MarkdownCodeBlockBackground"),
            BorderBrush = context.GetBrush("MarkdownCodeBlockBorderBrush"),
            BorderThickness = context.GetThickness("MarkdownCodeBlockBorderThickness", new Thickness(1)),
            CornerRadius = context.GetCornerRadius("MarkdownCodeBlockCornerRadius", new CornerRadius(8)),
            Margin = context.GetThickness("MarkdownBlockMargin", new Thickness(0, 0, 0, 12)),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var outerPanel = new DockPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        // Header with language label and copy button
        var header = new DockPanel
        {
            Background = context.GetBrush("MarkdownCodeBlockHeaderBackground"),
            LastChildFill = true,
        };

        var langLabel = new TextBlock
        {
            Text = string.IsNullOrEmpty(language) ? "Code" : language,
            Foreground = context.GetBrush("MarkdownCodeBlockHeaderForeground"),
            FontSize = context.GetDouble("MarkdownSmallFontSize", 12),
            FontFamily = context.GetFontFamily("MarkdownCodeFontFamily"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = context.GetThickness("MarkdownCodeBlockHeaderPadding", new Thickness(12, 8)),
        };

        var copyButton = new Button
        {
            Content = CreateCopyButtonContent(context, false),
            FontSize = context.GetDouble("MarkdownSmallFontSize", 12),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(8, 4),
        };

        copyButton.Click += async (sender, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(copyButton);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(code);
                if (sender is Button btn)
                {
                    btn.Content = CreateCopyButtonContent(context, true);
                    await Task.Delay(1500);
                    btn.Content = CreateCopyButtonContent(context, false);
                }
            }
            context.OnCodeCopied(code, language);
        };

        DockPanel.SetDock(langLabel, Dock.Left);
        DockPanel.SetDock(copyButton, Dock.Right);
        header.Children.Add(copyButton);
        header.Children.Add(langLabel);

        DockPanel.SetDock(header, Dock.Top);
        outerPanel.Children.Add(header);

        // Code content
        var codeTextBlock = new SelectableTextBlock
        {
            FontFamily = context.GetFontFamily("MarkdownCodeFontFamily"),
            FontSize = context.GetDouble("MarkdownCodeFontSize", 13),
            Foreground = context.GetBrush("MarkdownCodeBlockForeground"),
            TextWrapping = TextWrapping.NoWrap,
            Padding = context.GetThickness("MarkdownCodeBlockPadding", new Thickness(16)),
        };
        context.ApplySelectableTextStyle(codeTextBlock);
        codeTextBlock.Inlines ??= new InlineCollection();

        // Try syntax highlighting
        if (!string.IsNullOrEmpty(language) && context.SyntaxHighlighter != null)
        {
            try
            {
                var highlighted = context.SyntaxHighlighter.Highlight(code, language);
                foreach (var inline in highlighted)
                {
                    codeTextBlock.Inlines.Add(inline);
                }

                if (codeTextBlock.Inlines.Count == 0)
                    codeTextBlock.Text = code;
            }
            catch
            {
                codeTextBlock.Text = code;
            }
        }
        else
        {
            codeTextBlock.Text = code;
        }

        var scrollViewer = new ScrollViewer
        {
            Content = codeTextBlock,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        outerPanel.Children.Add(scrollViewer);
        container.Child = outerPanel;

        return container;
    }

    private static string ExtractCode(FencedCodeBlock block)
    {
        var lines = block.Lines;
        var code = lines.ToString();
        return code.TrimEnd('\n', '\r');
    }

    private static string NormalizeInfoToken(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
            return string.Empty;

        var trimmed = info.Trim();
        var splitIndex = trimmed.IndexOfAny([' ', '\t', '{']);
        return splitIndex > 0 ? trimmed[..splitIndex].Trim() : trimmed;
    }

    private const string CopyIconPath =
        "M503.458 21.333h-238.916c-52.877 0-82.002 0-109.458 14.063-23.834 12.292-43.541 32.021-55.667 55.667" +
        "-14.084 27.477-14.083 56.606-14.083 109.478v238.916c0 52.877-0.002 82.001 14.125 109.521 12.083 23.603" +
        " 31.791 43.334 55.5 55.563 27.584 14.127 56.707 14.125 109.583 14.125h76.791v76.791c0 52.877-0.002 82.001" +
        " 14.125 109.521 12.083 23.603 31.791 43.334 55.5 55.563 27.582 14.127 56.707 14.125 109.583 14.125h238.916" +
        "c52.877 0 82.001 0 109.457-14.063 23.834-12.292 43.541-32.021 55.667-55.667 14.084-27.477 14.084-56.607" +
        " 14.084-109.479v-238.916c0-52.877 0-82.001-14.127-109.521-12.083-23.603-31.787-43.332-55.497-55.561" +
        "-27.584-14.127-56.708-14.127-109.585-14.127h-76.791v-76.791c0-52.877 0-82.001-14.127-109.521" +
        "-12.083-23.603-31.787-43.332-55.497-55.561-27.58-14.127-56.708-14.127-109.585-14.127z" +
        "M264.542 533.333c-37.001 0-61.418 0.002-70.625-4.689-7.748-4.002-14.54-10.79-18.5-18.581" +
        "-4.749-9.229-4.75-33.647-4.75-70.605v-238.916c0-36.958-0.002-61.376 4.708-70.562 4.002-7.834" +
        " 10.79-14.626 18.667-18.688 9.084-4.625 33.504-4.625 70.5-4.625h238.916c37.001 0 61.419 0 70.626 4.689" +
        " 7.748 4.002 14.541 10.79 18.5 18.581 4.749 9.229 4.749 33.647 4.749 70.605v238.916c0 36.958 0 61.376" +
        "-4.71 70.562-4.002 7.834-10.79 14.626-18.667 18.688-9.084 4.625-33.502 4.625-70.498 4.625h-238.916z" +
        "M682.667 362.667h76.791c37.001 0 61.419 0 70.626 4.689 7.748 4.002 14.541 10.79 18.5 18.581" +
        " 4.749 9.229 4.749 33.647 4.749 70.605v238.916c0 36.958 0 61.376-4.71 70.563-4.002 7.834" +
        "-10.79 14.626-18.667 18.688-9.084 4.625-33.502 4.625-70.498 4.625h-238.916c-37.001 0-61.419 0.002" +
        "-70.626-4.687-7.748-4.002-14.541-10.793-18.5-18.583-4.749-9.229-4.749-33.647-4.749-70.604v-76.791" +
        "h76.791c52.877 0 82.001 0 109.457-14.063 23.834-12.292 43.541-32.021 55.667-55.667 14.084-27.478" +
        " 14.084-56.607 14.084-109.479v-76.791z";

    private const string CopyDoneIconPath =
        "M18.71,7.21a1,1,0,0,0-1.42,0L9.84,14.67,6.71,11.53A1,1,0,1,0,5.29,13l3.84,3.84a1,1,0,0,0,1.42," +
        "0l8.16-8.16A1,1,0,0,0,18.71,7.21Z";

    private static Control CreateCopyButtonContent(RenderContext context, bool copied)
    {
        var iconPath = copied ? CopyDoneIconPath : CopyIconPath;
        var icon = new ShapePath
        {
            Data = StreamGeometry.Parse(iconPath),
            Fill = context.GetBrush("MarkdownCodeBlockHeaderForeground"),
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var label = new TextBlock
        {
            Text = copied ? "已复制" : "复制",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        };
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.Children.Add(icon);
        panel.Children.Add(label);
        return panel;
    }
}
