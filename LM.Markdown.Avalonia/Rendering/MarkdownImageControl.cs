using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdig.Syntax.Inlines;

namespace LM.Markdown.Avalonia.Rendering;

internal sealed class MarkdownImageControl : Border
{
    private readonly RenderContext _context;
    private readonly bool _isBlockImage;
    private readonly Image _image;
    private readonly TextBlock _placeholder;
    private CancellationTokenSource? _loadCancellation;
    private bool _loadStarted;
    private bool _loadCompleted;

    private MarkdownImageControl(RenderContext context, string sourceUrl, string altText, bool isBlockImage)
    {
        _context = context;
        _isBlockImage = isBlockImage;
        SourceUrl = sourceUrl;
        AltText = altText;

        _image = new Image
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both,
            HorizontalAlignment = isBlockImage ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        _placeholder = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(altText) ? "Loading image..." : altText,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
        };

        Child = _placeholder;
        HorizontalAlignment = isBlockImage ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        ClipToBounds = false;
        Cursor = new Cursor(StandardCursorType.Hand);

        PointerPressed += (_, _) => _context.OnImageClicked(SourceUrl, AltText);
    }

    public string SourceUrl { get; }

    public string AltText { get; }

    public static MarkdownImageControl Create(LinkInline inline, RenderContext context, bool isBlockImage)
    {
        var source = inline.Url ?? string.Empty;
        var altText = ExtractAltText(inline);
        var control = new MarkdownImageControl(context, source, altText, isBlockImage);
        control.StartLoading();
        return control;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartLoading();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _loadCancellation?.Cancel();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!_isBlockImage)
        {
            _image.MaxHeight = ResolveInlineImageMaxHeight();
            _image.MaxWidth = double.IsInfinity(availableSize.Width)
                ? double.PositiveInfinity
                : Math.Max(0, availableSize.Width);

            return base.MeasureOverride(availableSize);
        }

        var child = Child;
        if (child == null)
            return default;

        var targetSize = CalculateBlockImageSize(availableSize);
        child.Measure(targetSize);
        return new Size(targetSize.Width, child.DesiredSize.Height > 0 ? child.DesiredSize.Height : targetSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!_isBlockImage)
            return base.ArrangeOverride(finalSize);

        var child = Child;
        if (child == null)
            return finalSize;

        var targetSize = CalculateBlockImageSize(finalSize);
        var arrangedHeight = child.DesiredSize.Height > 0 ? Math.Max(child.DesiredSize.Height, targetSize.Height) : targetSize.Height;
        child.Arrange(new Rect(0, 0, targetSize.Width, arrangedHeight));

        return new Size(finalSize.Width, arrangedHeight);
    }

    private void StartLoading()
    {
        if (_loadCompleted || _loadStarted || _context.ResourceLoader == null || string.IsNullOrWhiteSpace(SourceUrl))
            return;

        _loadStarted = true;
        var cancellationTokenSource = new CancellationTokenSource();
        _loadCancellation = cancellationTokenSource;
        LoadAsync(cancellationTokenSource);
    }

    private async void LoadAsync(CancellationTokenSource cancellationTokenSource)
    {
        var cancellationToken = cancellationTokenSource.Token;
        if (_context.ResourceLoader == null || string.IsNullOrWhiteSpace(SourceUrl))
            return;

        try
        {
            var loadedImage = await _context.ResourceLoader.LoadImageAsync(SourceUrl, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await Dispatcher.UIThread.InvokeAsync(new Action(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (loadedImage != null)
                {
                    _image.Source = loadedImage;
                    Child = _image;
                }
                else
                {
                    Child = CreateErrorText();
                }

                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
                (Parent as Layoutable)?.InvalidateMeasure();
                (Parent as Layoutable)?.InvalidateArrange();
                _loadCompleted = true;
            }));
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(new Action(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                Child = CreateErrorText();
                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
                (Parent as Layoutable)?.InvalidateMeasure();
                (Parent as Layoutable)?.InvalidateArrange();
                _loadCompleted = true;
            }));
        }
        finally
        {
            _loadStarted = false;

            if (ReferenceEquals(_loadCancellation, cancellationTokenSource))
            {
                cancellationTokenSource.Dispose();
                _loadCancellation = null;
            }
        }
    }

    private Size CalculateBlockImageSize(Size availableSize)
    {
        var widthConstraint = ResolveBlockWidthConstraint(availableSize.Width);
        var heightConstraint = double.IsInfinity(availableSize.Height)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Height);

        if (_image.Source?.Size is not { Width: > 0, Height: > 0 } imageSize)
        {
            return new Size(widthConstraint, heightConstraint);
        }

        var targetWidth = widthConstraint;
        var targetHeight = imageSize.Height * (targetWidth / imageSize.Width);

        var configuredMaxHeight = ResolveBlockImageMaxHeight();
        if (configuredMaxHeight > 0 && targetHeight > configuredMaxHeight)
        {
            targetHeight = configuredMaxHeight;
            targetWidth = imageSize.Width * (targetHeight / imageSize.Height);
        }

        if (!double.IsInfinity(heightConstraint) && targetHeight > heightConstraint)
        {
            targetHeight = heightConstraint;
            targetWidth = imageSize.Width * (targetHeight / imageSize.Height);
        }

        return new Size(Math.Max(0, targetWidth), Math.Max(0, targetHeight));
    }

    private double ResolveBlockWidthConstraint(double availableWidth)
    {
        var configuredMaxWidth = _context.GetDouble("MarkdownBlockImageMaxWidth", 0);
        var widthConstraint = double.IsInfinity(availableWidth)
            ? (configuredMaxWidth > 0 ? configuredMaxWidth : 600)
            : Math.Max(0, availableWidth);

        if (configuredMaxWidth > 0)
        {
            widthConstraint = Math.Min(widthConstraint, configuredMaxWidth);
        }

        return widthConstraint;
    }

    private double ResolveInlineImageMaxHeight()
    {
        var configuredMaxHeight = _context.GetDouble("MarkdownInlineImageMaxHeight", 260);
        return configuredMaxHeight > 0 ? configuredMaxHeight : double.PositiveInfinity;
    }

    private double ResolveBlockImageMaxHeight()
    {
        var configuredMaxHeight = _context.GetDouble("MarkdownBlockImageMaxHeight", 0);
        return configuredMaxHeight > 0 ? configuredMaxHeight : double.PositiveInfinity;
    }

    private TextBlock CreateErrorText()
    {
        return new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(AltText) ? "Image unavailable" : AltText,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            Foreground = _context.GetBrush("MarkdownMutedForeground", Brushes.Gray),
        };
    }

    private static string ExtractAltText(LinkInline inline)
    {
        var altText = string.Empty;
        var child = inline.FirstChild;
        while (child != null)
        {
            if (child is LiteralInline literal)
                altText += literal.Content.ToString();

            child = child.NextSibling;
        }

        return altText;
    }
}
