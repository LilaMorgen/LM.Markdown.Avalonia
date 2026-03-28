using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using LM.Markdown.Avalonia.Events;
using LM.Markdown.Avalonia.Parsing;
using LM.Markdown.Avalonia.Rendering;
using LM.Markdown.Avalonia.Services;
using Markdig;
using Markdig.Syntax;

namespace LM.Markdown.Avalonia.Controls;

public class MarkdownViewer : Control
{
    private const double AutoScrollBottomTolerance = 2.0;

    private StackPanel? _contentPanel;
    private ScrollViewer? _scrollViewer;
    private MarkdownDocumentRenderer? _renderer;
    private MarkdownPipeline? _pipeline;
    private MarkdownDocument? _currentDocument;
    private string _currentMarkdown = string.Empty;
    private CrossBlockSelectionHandler? _selectionHandler;
    private IDisposable? _scrollOffsetSubscription;
    private bool _stickToBottom = true;

    private ISyntaxHighlighter? _syntaxHighlighter;
    private IResourceLoader? _resourceLoader;
    private IMathRenderer? _mathRenderer;
    private IMermaidRenderer? _mermaidRenderer;

    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Markdown));

    public static readonly StyledProperty<bool> AutoScrollProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(AutoScroll), defaultValue: true);

    public static readonly StyledProperty<bool> EnableUnifiedSelectionProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(EnableUnifiedSelection), defaultValue: true);

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public bool AutoScroll
    {
        get => GetValue(AutoScrollProperty);
        set => SetValue(AutoScrollProperty, value);
    }

    public bool EnableUnifiedSelection
    {
        get => GetValue(EnableUnifiedSelectionProperty);
        set => SetValue(EnableUnifiedSelectionProperty, value);
    }

    public event EventHandler<LinkClickedEventArgs>? LinkClicked;
    public event EventHandler<ImageClickedEventArgs>? ImageClicked;
    public event EventHandler<CodeBlockCopyEventArgs>? CodeCopied;

    public ISyntaxHighlighter? SyntaxHighlighter
    {
        get => _syntaxHighlighter;
        set
        {
            _syntaxHighlighter = value;
            InvalidateRenderer();
        }
    }

    public IResourceLoader? ResourceLoader
    {
        get => _resourceLoader;
        set
        {
            _resourceLoader = value;
            InvalidateRenderer();
        }
    }

    public IMathRenderer? MathRenderer
    {
        get => _mathRenderer;
        set
        {
            _mathRenderer = value;
            InvalidateRenderer();
        }
    }

    public IMermaidRenderer? MermaidRenderer
    {
        get => _mermaidRenderer;
        set
        {
            _mermaidRenderer = value;
            InvalidateRenderer();
        }
    }

    static MarkdownViewer()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownViewer>((viewer, _) => viewer.OnMarkdownChanged());
    }

    public MarkdownViewer()
    {
        _syntaxHighlighter = new TextMateSyntaxHighlighter();
        _resourceLoader = new DefaultResourceLoader();
        _mathRenderer = new MathRendererService();
        _mermaidRenderer = new MermaidRendererService();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureVisualTree();
        OnMarkdownChanged();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _scrollOffsetSubscription?.Dispose();
        _scrollOffsetSubscription = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void EnsureVisualTree()
    {
        if (_contentPanel != null)
        {
            EnsureScrollOffsetSubscription();
            return;
        }

        _contentPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _contentPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        EnsureScrollOffsetSubscription();

        VisualChildren.Add(_scrollViewer);
        LogicalChildren.Add(_scrollViewer);

        if (EnableUnifiedSelection)
        {
            _selectionHandler = new CrossBlockSelectionHandler(_contentPanel, _scrollViewer);
        }
    }

    private void EnsureScrollOffsetSubscription()
    {
        if (_scrollViewer == null || _scrollOffsetSubscription != null)
            return;

        _scrollOffsetSubscription = _scrollViewer
            .GetObservable(ScrollViewer.OffsetProperty)
            .Subscribe(_ => UpdateStickToBottomState());
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_contentPanel != null && !double.IsInfinity(availableSize.Width))
        {
            _contentPanel.Width = Math.Max(0, availableSize.Width);
        }

        _scrollViewer?.Measure(availableSize);
        return _scrollViewer?.DesiredSize ?? base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_contentPanel != null)
        {
            _contentPanel.Width = Math.Max(0, finalSize.Width);
        }

        _scrollViewer?.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public void AppendMarkdown(string chunk)
    {
        if (string.IsNullOrEmpty(chunk))
            return;

        var previousMarkdown = _currentMarkdown;
        _currentMarkdown += chunk;
        RenderIncremental(previousMarkdown, _currentMarkdown);
    }

    public void ClearMarkdown()
    {
        _currentMarkdown = string.Empty;
        _currentDocument = null;
        _stickToBottom = true;
        SetValue(MarkdownProperty, null);
        _contentPanel?.Children.Clear();
    }

    public void RegisterBlockRenderer(IBlockRenderer renderer)
    {
        EnsureRenderer();
        _renderer?.Context.RegisterBlockRenderer(renderer);
    }

    public void RegisterInlineRenderer(IInlineRenderer renderer)
    {
        EnsureRenderer();
        _renderer?.Context.RegisterInlineRenderer(renderer);
    }

    private void OnMarkdownChanged()
    {
        var markdown = Markdown;
        if (_contentPanel == null)
            return;

        if (string.IsNullOrEmpty(markdown))
        {
            _currentMarkdown = string.Empty;
            _currentDocument = null;
            _contentPanel.Children.Clear();
            return;
        }

        _currentMarkdown = markdown;
        RenderFull(markdown);
    }

    private void RenderFull(string markdown)
    {
        if (_contentPanel == null)
            return;

        EnsureRenderer();
        var shouldAutoScroll = ShouldAutoScrollAfterRender();

        try
        {
            _pipeline ??= MarkdownPipelineFactory.Create();
            var document = MarkdownPipelineFactory.Parse(markdown, _pipeline);
            var controls = _renderer!.Render(document);

            _contentPanel.Children.Clear();
            foreach (var control in controls)
            {
                _contentPanel.Children.Add(control);
            }

            _currentDocument = document;

            // Update selection handler with source mapping so Ctrl+C produces markdown source
            _selectionHandler?.UpdateSourceMapping(markdown, _renderer!.Context.SourceSpanMap);

            if (shouldAutoScroll)
                ScrollToEnd();
        }
        catch
        {
            _contentPanel.Children.Clear();
            _contentPanel.Children.Add(new TextBlock
            {
                Text = markdown,
                TextWrapping = TextWrapping.Wrap,
            });

                }
            }

    private void RenderIncremental(string previousMarkdown, string markdown)
    {
        if (_contentPanel == null)
            return;

        EnsureRenderer();
        var shouldAutoScroll = ShouldAutoScrollAfterRender();

        if (_currentDocument == null)
        {
            RenderFull(markdown);
            return;
        }

        try
        {
            _pipeline ??= MarkdownPipelineFactory.Create();
            var document = MarkdownPipelineFactory.Parse(markdown, _pipeline);
            var (firstChanged, newControls) = _renderer!.RenderIncremental(_currentDocument, previousMarkdown, document, markdown);

            if (firstChanged > _contentPanel.Children.Count)
            {
                RenderFull(markdown);
                return;
            }

            while (_contentPanel.Children.Count > firstChanged)
            {
                _contentPanel.Children.RemoveAt(_contentPanel.Children.Count - 1);
            }

            foreach (var control in newControls)
            {
                _contentPanel.Children.Add(control);
            }

            _currentDocument = document;
            UpdateSourceMapping(markdown, document);

            if (shouldAutoScroll)
                ScrollToEnd();
        }
        catch
        {
            RenderFull(markdown);
        }
    }

    private void UpdateSourceMapping(string markdown, MarkdownDocument document)
    {
        if (_renderer == null || _contentPanel == null)
            return;

        _renderer.Context.ClearSourceSpanMap();

        var childIndex = 0;
        foreach (var block in document)
        {
            if (block.GetType().Name == "LinkReferenceDefinitionGroup")
                continue;

            if (childIndex >= _contentPanel.Children.Count)
                break;

            if (_contentPanel.Children[childIndex] is Control control)
            {
                _renderer.Context.SetSourceSpan(control, block.Span.Start, block.Span.End);
            }

            childIndex++;
        }

        _selectionHandler?.UpdateSourceMapping(markdown, _renderer.Context.SourceSpanMap);
    }

    private void ScrollToEnd()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!AutoScroll || !_stickToBottom)
                return;

            _scrollViewer?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    private bool ShouldAutoScrollAfterRender()
    {
        return AutoScroll && (_scrollViewer == null || _stickToBottom || IsScrolledToBottom());
    }

    private void UpdateStickToBottomState()
    {
        _stickToBottom = IsScrolledToBottom();
    }

    private bool IsScrolledToBottom()
    {
        if (_scrollViewer == null)
            return true;

        var maxOffset = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        if (maxOffset <= AutoScrollBottomTolerance)
            return true;

        return maxOffset - _scrollViewer.Offset.Y <= AutoScrollBottomTolerance;
    }

    private void EnsureRenderer()
    {
        if (_renderer != null)
            return;

        EnsureVisualTree();
        CreateRenderer();
    }

    private void CreateRenderer()
    {
        var context = new RenderContext(this);
        context.SyntaxHighlighter = _syntaxHighlighter;
        context.ResourceLoader = _resourceLoader;
        context.MathRenderer = _mathRenderer;
        context.MermaidRenderer = _mermaidRenderer;

        context.LinkClicked += (s, e) => LinkClicked?.Invoke(this, e);
        context.ImageClicked += (s, e) => ImageClicked?.Invoke(this, e);
        context.CodeCopied += (s, e) => CodeCopied?.Invoke(this, e);

        _renderer = new MarkdownDocumentRenderer(context);

        UpdateThemeForServices();
    }

    private void InvalidateRenderer()
    {
        _renderer = null;
        if (!string.IsNullOrEmpty(_currentMarkdown) && _contentPanel != null)
        {
            RenderFull(_currentMarkdown);
        }
    }

    private void UpdateThemeForServices()
    {
        var isDark = ActualThemeVariant == ThemeVariant.Dark;

        if (_syntaxHighlighter is TextMateSyntaxHighlighter tmHighlighter)
            tmHighlighter.IsDark = isDark;

        if (_mermaidRenderer is MermaidRendererService mermaidService)
        {
            mermaidService.BgColor = isDark ? "#1C1F23" : "#FFFFFF";
            mermaidService.FgColor = isDark ? "#E0E2E6" : "#1C1F23";
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property.Name == "ActualThemeVariant")
        {
            UpdateThemeForServices();
            if (!string.IsNullOrEmpty(_currentMarkdown) && _contentPanel != null)
            {
                RenderFull(_currentMarkdown);
            }
        }
    }
}
