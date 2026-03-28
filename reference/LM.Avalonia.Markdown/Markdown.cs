using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;
using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Parsers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;
using AvaloniaSpan = Avalonia.Controls.Documents.Span;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace LM.Avalonia.Markdown;

[TemplatePart(Name = FlowDocumentRootPartName, Type = typeof(Decorator), IsRequired = true)]
public class Markdown : TemplatedControl
{
    private const string FlowDocumentRootPartName = "PART_FlowDocumentRoot";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Markdown, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
        TextBox.SelectionBrushProperty.AddOwner<Markdown>();

    public static readonly StyledProperty<int> SelectionStartProperty =
        TextBox.SelectionStartProperty.AddOwner<Markdown>();

    public static readonly StyledProperty<int> SelectionEndProperty =
        TextBox.SelectionEndProperty.AddOwner<Markdown>();

    public static readonly DirectProperty<Markdown, string> SelectedTextProperty =
        AvaloniaProperty.RegisterDirect<Markdown, string>(nameof(SelectedText), control => control.SelectedText);

    public static readonly DirectProperty<Markdown, bool> CanCopyProperty =
        TextBox.CanCopyProperty.AddOwner<Markdown>(control => control.CanCopy);

    public static readonly RoutedEvent<RoutedEventArgs> CopyingToClipboardEvent =
        RoutedEvent.Register<Markdown, RoutedEventArgs>(nameof(CopyingToClipboard), RoutingStrategies.Bubble);

    public static readonly StyledProperty<MarkdownImageLoader> ImageLoaderProperty =
        AvaloniaProperty.Register<Markdown, MarkdownImageLoader>(nameof(ImageLoader), new MarkdownImageLoader());

    public static readonly StyledProperty<CodeHighlighter?> CodeHighlighterProperty =
        AvaloniaProperty.Register<Markdown, CodeHighlighter?>(nameof(CodeHighlighter));

    public static readonly StyledProperty<bool> IsSelectionEnabledProperty =
        AvaloniaProperty.Register<Markdown, bool>(nameof(IsSelectionEnabled), true);

    private readonly List<TextSegment> _segments = [];
    private readonly List<IDisposable> _subscriptions = [];
    private Decorator? _flowRoot;
    private MarkdownPipeline? _pipeline;
    private string _selectedText = string.Empty;
    private string _plainText = string.Empty;
    private bool _canCopy;
    private bool _updatingSelectionFromChildren;
    private bool _updatingChildrenFromSelection;

    [Content]
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    public int SelectionStart
    {
        get => GetValue(SelectionStartProperty);
        set => SetValue(SelectionStartProperty, value);
    }

    public int SelectionEnd
    {
        get => GetValue(SelectionEndProperty);
        set => SetValue(SelectionEndProperty, value);
    }

    public string SelectedText => _selectedText;

    public MarkdownImageLoader ImageLoader
    {
        get => GetValue(ImageLoaderProperty);
        set => SetValue(ImageLoaderProperty, value);
    }

    public CodeHighlighter? CodeHighlighter
    {
        get => GetValue(CodeHighlighterProperty);
        set => SetValue(CodeHighlighterProperty, value);
    }

    public bool CanCopy
    {
        get => _canCopy;
        private set => SetAndRaise(CanCopyProperty, ref _canCopy, value);
    }

    public bool IsSelectionEnabled
    {
        get => GetValue(IsSelectionEnabledProperty);
        set => SetValue(IsSelectionEnabledProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? CopyingToClipboard
    {
        add => AddHandler(CopyingToClipboardEvent, value);
        remove => RemoveHandler(CopyingToClipboardEvent, value);
    }

    static Markdown()
    {
        FocusableProperty.OverrideDefaultValue<Markdown>(true);
    }

    public Markdown()
    {
        Template = new FuncControlTemplate<Markdown>((_, scope) =>
        {
            var border = new Border();
            border[!Border.BackgroundProperty] = new TemplateBinding(BackgroundProperty);
            border[!Border.BorderBrushProperty] = new TemplateBinding(BorderBrushProperty);
            border[!Border.BorderThicknessProperty] = new TemplateBinding(BorderThicknessProperty);
            border[!Border.CornerRadiusProperty] = new TemplateBinding(CornerRadiusProperty);

            var scrollViewer = new ScrollViewer();
            scrollViewer[!ScrollViewer.AllowAutoHideProperty] = new TemplateBinding(ScrollViewer.AllowAutoHideProperty);
            scrollViewer[!ScrollViewer.HorizontalScrollBarVisibilityProperty] = new TemplateBinding(ScrollViewer.HorizontalScrollBarVisibilityProperty);
            scrollViewer[!ScrollViewer.VerticalScrollBarVisibilityProperty] = new TemplateBinding(ScrollViewer.VerticalScrollBarVisibilityProperty);

            var root = new Decorator();
            root[!Layoutable.MarginProperty] = new TemplateBinding(PaddingProperty);
            scope.Register(FlowDocumentRootPartName, root);

            scrollViewer.Content = root;
            border.Child = scrollViewer;
            return border;
        });

        Padding = new Thickness(2);
        SetCurrentValue(SelectionBrushProperty, Brushes.DodgerBlue);
    }

    public async void Copy()
    {
        if (!CanCopy)
        {
            return;
        }

        var selectedText = SelectedText;
        if (string.IsNullOrEmpty(selectedText))
        {
            return;
        }

        var args = new RoutedEventArgs(CopyingToClipboardEvent);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(selectedText);
        }
    }

    public void SelectAll()
    {
        SetCurrentValue(SelectionStartProperty, 0);
        SetCurrentValue(SelectionEndProperty, _plainText.Length);
        ApplySelectionToChildren();
        UpdateSelectedText();
    }

    public void ClearSelection()
    {
        SetCurrentValue(SelectionEndProperty, SelectionStart);
        ApplySelectionToChildren();
        UpdateSelectedText();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_flowRoot is not null)
        {
            _flowRoot.Child = null;
        }

        _flowRoot = e.NameScope.Find<Decorator>(FlowDocumentRootPartName);
        RenderMarkdown();
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateSelectedText();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        if ((ContextFlyout == null || !ContextFlyout.IsOpen) && (ContextMenu == null || !ContextMenu.IsOpen))
        {
            ClearSelection();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var hotkeys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
        if (hotkeys is null)
        {
            return;
        }

        static bool Matches(KeyEventArgs args, IReadOnlyList<KeyGesture> gestures) => gestures.Any(gesture => gesture.Matches(args));

        if (Matches(e, hotkeys.Copy))
        {
            Copy();
            e.Handled = true;
            return;
        }

        if (Matches(e, hotkeys.SelectAll))
        {
            SelectAll();
            e.Handled = true;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CodeHighlighterProperty)
        {
            if (change.OldValue is CodeHighlighter oldHighlighter)
            {
                oldHighlighter.Invalidated -= CodeHighlighterOnInvalidated;
            }

            if (change.NewValue is CodeHighlighter newHighlighter)
            {
                newHighlighter.Invalidated += CodeHighlighterOnInvalidated;
            }

            RenderMarkdown();
            return;
        }

        if (change.Property == TextProperty || change.Property == ImageLoaderProperty)
        {
            RenderMarkdown();
            return;
        }

        if (change.Property == SelectionBrushProperty)
        {
            foreach (var segment in _segments)
            {
                segment.TextBlock.SelectionBrush = SelectionBrush;
            }

            return;
        }

        if (change.Property == IsSelectionEnabledProperty)
        {
            foreach (var segment in _segments)
            {
                segment.TextBlock.IsHitTestVisible = IsSelectionEnabled;
                segment.TextBlock.Focusable = IsSelectionEnabled;
            }

            return;
        }

        if (change.Property == SelectionStartProperty || change.Property == SelectionEndProperty)
        {
            if (!_updatingSelectionFromChildren)
            {
                ApplySelectionToChildren();
            }

            UpdateSelectedText();
        }
    }

    private void CodeHighlighterOnInvalidated(object? sender, EventArgs e)
    {
        RenderMarkdown();
    }

    private void RenderMarkdown()
    {
        if (_flowRoot is null)
        {
            return;
        }

        ClearSubscriptions();
        _segments.Clear();

        _pipeline ??= new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

        var builder = new MarkdownDocumentBuilder(ImageLoader, CodeHighlighter, SelectionBrush, IsSelectionEnabled);
        var result = builder.Build(Text, _pipeline);

        _flowRoot.Child = result.Root;
        _plainText = result.PlainText;
        _segments.AddRange(result.Segments);

        foreach (var segment in _segments)
        {
            void Handler(object? _, AvaloniaPropertyChangedEventArgs args)
            {
                if (args.Property == SelectableTextBlock.SelectionStartProperty || args.Property == SelectableTextBlock.SelectionEndProperty)
                {
                    SyncSelectionFromChild(segment);
                }
            }

            segment.TextBlock.PropertyChanged += Handler;
            _subscriptions.Add(new AnonymousDisposable(() => segment.TextBlock.PropertyChanged -= Handler));
        }

        ApplySelectionToChildren();
        UpdateSelectedText();
    }

    private void SyncSelectionFromChild(TextSegment sourceSegment)
    {
        if (_updatingChildrenFromSelection)
        {
            return;
        }

        _updatingSelectionFromChildren = true;
        try
        {
            var localStart = Math.Clamp(sourceSegment.TextBlock.SelectionStart, 0, sourceSegment.Length);
            var localEnd = Math.Clamp(sourceSegment.TextBlock.SelectionEnd, 0, sourceSegment.Length);
            SetCurrentValue(SelectionStartProperty, sourceSegment.Start + localStart);
            SetCurrentValue(SelectionEndProperty, sourceSegment.Start + localEnd);
        }
        finally
        {
            _updatingSelectionFromChildren = false;
        }

        UpdateSelectedText();
    }

    private void ApplySelectionToChildren()
    {
        if (_segments.Count == 0)
        {
            return;
        }

        var start = Math.Clamp(SelectionStart, 0, _plainText.Length);
        var end = Math.Clamp(SelectionEnd, 0, _plainText.Length);
        var min = Math.Min(start, end);
        var max = Math.Max(start, end);

        _updatingChildrenFromSelection = true;
        try
        {
            foreach (var segment in _segments)
            {
                var overlapStart = Math.Max(min, segment.Start);
                var overlapEnd = Math.Min(max, segment.Start + segment.Length);
                if (overlapEnd <= overlapStart)
                {
                    segment.TextBlock.SelectionStart = 0;
                    segment.TextBlock.SelectionEnd = 0;
                    continue;
                }

                segment.TextBlock.SelectionStart = overlapStart - segment.Start;
                segment.TextBlock.SelectionEnd = overlapEnd - segment.Start;
            }
        }
        finally
        {
            _updatingChildrenFromSelection = false;
        }
    }

    private void UpdateSelectedText()
    {
        var start = Math.Clamp(Math.Min(SelectionStart, SelectionEnd), 0, _plainText.Length);
        var end = Math.Clamp(Math.Max(SelectionStart, SelectionEnd), 0, _plainText.Length);
        var selection = end > start ? _plainText[start..end] : string.Empty;
        SetAndRaise(SelectedTextProperty, ref _selectedText, selection);
        CanCopy = !string.IsNullOrEmpty(selection);
    }

    private void ClearSubscriptions()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
    }

    private sealed record TextSegment(SelectableTextBlock TextBlock, int Start, int Length);

    private sealed record MarkdownRenderResult(Control Root, ReadOnlyCollection<TextSegment> Segments, string PlainText);

    private sealed class MarkdownDocumentBuilder(
        MarkdownImageLoader imageLoader,
        CodeHighlighter? codeHighlighter,
        IBrush? selectionBrush,
        bool isSelectionEnabled)
    {
        private readonly List<TextSegment> _segments = [];
        private readonly StringBuilder _plainText = new();

        public MarkdownRenderResult Build(string? markdown, MarkdownPipeline pipeline)
        {
            var document = Markdig.Markdown.Parse(markdown ?? string.Empty, pipeline);
            var root = new StackPanel
            {
                Spacing = 8
            };

            foreach (var block in document)
            {
                var control = BuildBlock(block);
                if (control is not null)
                {
                    root.Children.Add(control);
                }
            }

            return new MarkdownRenderResult(root, _segments.AsReadOnly(), _plainText.ToString());
        }

        private Control? BuildBlock(Block block)
        {
            return block switch
            {
                HeadingBlock heading => BuildHeading(heading),
                ParagraphBlock paragraph => BuildParagraph(paragraph, defaultMargin: new Thickness(0)),
                AlertBlock alert => BuildAlert(alert),
                QuoteBlock quote => BuildQuote(quote),
                ListBlock list => BuildList(list),
                ThematicBreakBlock => BuildThematicBreak(),
                Table table => BuildTable(table),
                CodeBlock codeBlock => BuildCodeBlock(codeBlock),
                FootnoteGroup footnoteGroup => BuildFootnoteGroup(footnoteGroup),
                HtmlBlock htmlBlock => BuildPlainTextBlock(GetLeafText(htmlBlock), new Thickness(0), wrap: false),
                ContainerBlock container => BuildContainer(container),
                LeafBlock leaf => BuildParagraph(leaf, defaultMargin: new Thickness(0)),
                _ => null
            };
        }

        private Control? BuildContainer(ContainerBlock container)
        {
            var panel = new StackPanel
            {
                Spacing = 6
            };

            foreach (var child in container)
            {
                var control = BuildBlock(child);
                if (control is not null)
                {
                    panel.Children.Add(control);
                }
            }

            return panel.Children.Count == 0 ? null : panel;
        }

        private Control BuildHeading(HeadingBlock block)
        {
            var textBlock = CreateTextBlock();
            textBlock.Margin = new Thickness(0, 4, 0, 2);
            textBlock.FontWeight = FontWeight.Bold;
            textBlock.FontSize = block.Level switch
            {
                1 => 30,
                2 => 24,
                3 => 20,
                4 => 18,
                5 => 16,
                _ => 14
            };

            AppendLeafInlines(block, textBlock.Inlines!);
            RegisterText(textBlock, Environment.NewLine);
            return textBlock;
        }

        private Control BuildParagraph(LeafBlock block, Thickness defaultMargin)
        {
            var textBlock = CreateTextBlock();
            textBlock.Margin = defaultMargin;
            AppendLeafInlines(block, textBlock.Inlines!);
            RegisterText(textBlock, Environment.NewLine);
            return textBlock;
        }

        private Control BuildQuote(QuoteBlock quote)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.Parse("#94A3B8")),
                BorderThickness = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(12, 4, 0, 4),
                Margin = new Thickness(0, 2, 0, 2)
            };

            var panel = new StackPanel
            {
                Spacing = 6
            };
            foreach (var child in quote)
            {
                var control = BuildBlock(child);
                if (control is not null)
                {
                    panel.Children.Add(control);
                }
            }

            border.Child = panel;
            return border;
        }

        private Control BuildAlert(AlertBlock alert)
        {
            var kind = alert.Kind.ToString().ToLowerInvariant();
            var accent = kind switch
            {
                "note" => Color.Parse("#2563EB"),
                "tip" => Color.Parse("#059669"),
                "important" => Color.Parse("#7C3AED"),
                "warning" => Color.Parse("#D97706"),
                "caution" => Color.Parse("#DC2626"),
                _ => Color.Parse("#475569")
            };

            var border = new Border
            {
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(accent, 0.08),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 2, 0, 2)
            };

            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new TextBlock
            {
                Text = alert.Kind.ToString(),
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(accent)
            });

            foreach (var child in alert)
            {
                var control = BuildBlock(child);
                if (control is not null)
                {
                    panel.Children.Add(control);
                }
            }

            border.Child = panel;
            return border;
        }

        private Control BuildFootnoteGroup(FootnoteGroup group)
        {
            var panel = new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(0, 8, 0, 0)
            };
            panel.Children.Add(BuildThematicBreak());

            foreach (var child in group)
            {
                var control = BuildBlock(child);
                if (control is not null)
                {
                    panel.Children.Add(control);
                }
            }

            return panel;
        }

        private Control BuildList(ListBlock list)
        {
            var panel = new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(0, 2, 0, 2)
            };

            var index = list.IsOrdered ? ParseOrderedStart(list) : 1;
            foreach (var item in list)
            {
                if (item is not ListItemBlock listItem)
                {
                    continue;
                }

                var grid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    ColumnSpacing = 8
                };

                var marker = new TextBlock
                {
                    Text = list.IsOrdered ? $"{index}." : "•",
                    FontWeight = FontWeight.SemiBold,
                    Margin = new Thickness(0, 2, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };
                Grid.SetColumn(marker, 0);
                grid.Children.Add(marker);

                var content = new StackPanel
                {
                    Spacing = 4
                };
                Grid.SetColumn(content, 1);
                grid.Children.Add(content);

                foreach (var child in listItem)
                {
                    var control = BuildBlock(child);
                    if (control is not null)
                    {
                        content.Children.Add(control);
                    }
                }

                panel.Children.Add(grid);
                index++;
            }

            return panel;
        }

        private Control BuildTable(Table table)
        {
            var border = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 4, 0, 4)
            };

            var grid = new Grid
            {
                RowSpacing = 0,
                ColumnSpacing = 0
            };

            var columnCount = Math.Max(1, table.ColumnDefinitions.Count);
            for (var i = 0; i < columnCount; i++)
            {
                var width = GridLength.Star;
                if (i < table.ColumnDefinitions.Count && table.ColumnDefinitions[i].Width > 0)
                {
                    width = new GridLength(table.ColumnDefinitions[i].Width, GridUnitType.Star);
                }

                grid.ColumnDefinitions.Add(new ColumnDefinition(width));
            }

            var rowIndex = 0;
            foreach (var rowBlock in table)
            {
                if (rowBlock is not TableRow row)
                {
                    continue;
                }

                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                var columnIndex = 0;
                foreach (var cellBlock in row)
                {
                    if (cellBlock is not TableCell cell)
                    {
                        continue;
                    }

                    var cellBorder = new Border
                    {
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Background = row.IsHeader ? new SolidColorBrush(Color.Parse("#F8FAFC")) : Brushes.Transparent,
                        Padding = new Thickness(8)
                    };

                    var cellContent = new StackPanel { Spacing = 4 };
                    foreach (var child in cell)
                    {
                        var control = BuildBlock(child);
                        if (control is not null)
                        {
                            cellContent.Children.Add(control);
                        }
                    }

                    cellBorder.Child = cellContent;
                    Grid.SetRow(cellBorder, rowIndex);
                    Grid.SetColumn(cellBorder, columnIndex);
                    if (cell.ColumnSpan > 1)
                    {
                        Grid.SetColumnSpan(cellBorder, cell.ColumnSpan);
                    }

                    if (cell.RowSpan > 1)
                    {
                        Grid.SetRowSpan(cellBorder, cell.RowSpan);
                    }

                    if (columnIndex < table.ColumnDefinitions.Count)
                    {
                        cellContent.HorizontalAlignment = table.ColumnDefinitions[columnIndex].Alignment switch
                        {
                            TableColumnAlign.Center => HorizontalAlignment.Center,
                            TableColumnAlign.Right => HorizontalAlignment.Right,
                            _ => HorizontalAlignment.Stretch
                        };
                    }

                    grid.Children.Add(cellBorder);
                    columnIndex += Math.Max(cell.ColumnSpan, 1);
                }

                rowIndex++;
            }

            border.Child = grid;
            return border;
        }

        private Control BuildCodeBlock(CodeBlock codeBlock)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#0F172A"), 0.96),
                BorderBrush = new SolidColorBrush(Color.Parse("#334155")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 4, 0, 4)
            };

            var panel = new StackPanel
            {
                Spacing = 0
            };

            var language = GetCodeLanguage(codeBlock);
            if (codeHighlighter is not null && codeBlock is FencedCodeBlock fenced && fenced.Lines.Lines is not null)
            {
                codeHighlighter.BeginBlock(language);
                try
                {
                    for (var i = 0; i < fenced.Lines.Count; i++)
                    {
                        var line = fenced.Lines.Lines[i];
                        var result = codeHighlighter.Highlight(line, language);
                        if (result is not null)
                        {
                            panel.Children.Add(result.BuildControl());
                        }
                        else
                        {
                            panel.Children.Add(BuildCodeLine(line.ToString()));
                        }
                    }
                }
                finally
                {
                    codeHighlighter.EndBlock();
                }
            }
            else
            {
                panel.Children.Add(BuildCodeLine(GetLeafText(codeBlock)));
            }

            border.Child = panel;
            return border;
        }

        private Control BuildCodeLine(string text)
        {
            var block = CreateTextBlock();
            block.TextWrapping = TextWrapping.NoWrap;
            block.FontFamily = new FontFamily("Cascadia Mono,Consolas,Courier New");
            block.Foreground = Brushes.WhiteSmoke;
            block.Text = text;
            RegisterText(block, Environment.NewLine);
            return block;
        }

        private Control BuildThematicBreak()
        {
            return new Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(Color.Parse("#CBD5E1")),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 6, 0, 6)
            };
        }

        private Control BuildPlainTextBlock(string text, Thickness margin, bool wrap)
        {
            var block = CreateTextBlock();
            block.Margin = margin;
            block.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            block.Text = text;
            RegisterText(block, Environment.NewLine);
            return block;
        }

        private SelectableTextBlock CreateTextBlock()
        {
            return new SelectableTextBlock
            {
                SelectionBrush = selectionBrush,
                SelectionForegroundBrush = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                IsHitTestVisible = isSelectionEnabled,
                Focusable = isSelectionEnabled
            };
        }

        private void RegisterText(SelectableTextBlock textBlock, string trailingText)
        {
            var text = textBlock.Inlines?.Text ?? textBlock.Text ?? string.Empty;
            var start = _plainText.Length;
            _plainText.Append(text);
            _segments.Add(new TextSegment(textBlock, start, text.Length));
            _plainText.Append(trailingText);
        }

        private void AppendLeafInlines(LeafBlock block, InlineCollection inlines)
        {
            if (block.Inline is null)
            {
                AppendText(inlines, GetLeafText(block));
                return;
            }

            for (var inline = block.Inline.FirstChild; inline is not null; inline = inline.NextSibling)
            {
                AppendInline(inlines, inline);
            }
        }

        private void AppendInline(InlineCollection inlines, MarkdigInline inline)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    AppendText(inlines, literal.Content.ToString());
                    break;
                case LineBreakInline lineBreak:
                    if (lineBreak.IsHard)
                    {
                        inlines.Add(new LineBreak());
                    }
                    else
                    {
                        AppendText(inlines, " ");
                    }

                    break;
                case CodeInline codeInline:
                {
                    var run = new Run(codeInline.Content)
                    {
                        Background = new SolidColorBrush(Color.Parse("#E2E8F0")),
                        FontFamily = new FontFamily("Cascadia Mono,Consolas,Courier New")
                    };
                    inlines.Add(run);
                    break;
                }
                case EmphasisInline emphasis:
                    AppendEmphasis(inlines, emphasis);
                    break;
                case LinkInline link:
                    AppendLink(inlines, link);
                    break;
                case DelimiterInline delimiter:
                    AppendText(inlines, delimiter.ToLiteral());
                    for (var child = delimiter.FirstChild; child is not null; child = child.NextSibling)
                    {
                        AppendInline(inlines, child);
                    }

                    break;
                case FootnoteLink footnoteLink:
                    AppendFootnoteLink(inlines, footnoteLink);
                    break;
                case ContainerInline container:
                    for (var child = container.FirstChild; child is not null; child = child.NextSibling)
                    {
                        AppendInline(inlines, child);
                    }

                    break;
            }
        }

        private void AppendFootnoteLink(InlineCollection inlines, FootnoteLink footnoteLink)
        {
            var label = footnoteLink.Footnote?.Label?.TrimStart('^') ?? "?";
            var run = new Run(label)
            {
                BaselineAlignment = BaselineAlignment.TextTop,
                FontSize = 10,
                TextDecorations = new TextDecorationCollection()
            };

            inlines.Add(run);
        }

        private void AppendEmphasis(InlineCollection inlines, EmphasisInline emphasis)
        {
            AvaloniaSpan? span = emphasis.DelimiterChar switch
            {
                '*' or '_' => emphasis.DelimiterCount == 2 ? new Bold() : new Italic(),
                '+' => new Underline(),
                '=' => new AvaloniaSpan { Background = new SolidColorBrush(Color.Parse("#FEF3C7")) },
                '~' => new AvaloniaSpan(),
                '^' => new AvaloniaSpan { BaselineAlignment = BaselineAlignment.TextTop, FontSize = 10 },
                _ => null
            };

            if (span is null)
            {
                for (var child = emphasis.FirstChild; child is not null; child = child.NextSibling)
                {
                    AppendInline(inlines, child);
                }

                return;
            }

            if (emphasis.DelimiterChar == '~' && emphasis.DelimiterCount == 2)
            {
                span.TextDecorations = TextDecorations.Strikethrough;
            }

            for (var child = emphasis.FirstChild; child is not null; child = child.NextSibling)
            {
                AppendInline(span.Inlines, child);
            }

            inlines.Add(span);
        }

        private void AppendLink(InlineCollection inlines, LinkInline link)
        {
            var url = link.GetDynamicUrl?.Invoke() ?? link.Url;
            var tooltip = !string.IsNullOrWhiteSpace(link.Title) ? link.Title : url;

            if (link.IsImage)
            {
                var image = new MarkdownImageControl(imageLoader)
                {
                    SourceUrl = url,
                    MaxHeight = 260,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(2),
                    [ToolTip.TipProperty] = tooltip
                };
                inlines.Add(new InlineUIContainer(image));
                return;
            }

            var button = new HyperlinkButton
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                [ToolTip.TipProperty] = tooltip
            };

            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var navigateUri))
            {
                button.NavigateUri = navigateUri;
            }

            var content = new TextBlock
            {
                TextWrapping = TextWrapping.NoWrap
            };

            var contentInlines = content.Inlines!;

            for (var child = link.FirstChild; child is not null; child = child.NextSibling)
            {
                AppendInline(contentInlines, child);
            }

            if (contentInlines.Count == 0)
            {
                content.Text = url ?? string.Empty;
            }

            button.Content = content;
            inlines.Add(new InlineUIContainer(button));
        }

        private static void AppendText(InlineCollection inlines, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                inlines.Add(new Run(text));
            }
        }

        private static string GetLeafText(LeafBlock block)
        {
            if (block.Lines.Lines is null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < block.Lines.Count; i++)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(block.Lines.Lines[i].Slice.ToString());
            }

            return builder.ToString();
        }

        private static string? GetCodeLanguage(CodeBlock codeBlock)
        {
            if (codeBlock is not FencedCodeBlock fenced)
            {
                return null;
            }

            var parser = codeBlock.Parser as FencedCodeBlockParser;
            var infoPrefix = parser?.InfoPrefix ?? string.Empty;
            var info = fenced.Info?.Replace(infoPrefix, string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(info))
            {
                return null;
            }

            return info.Trim();
        }

        private static int ParseOrderedStart(ListBlock list)
        {
            if (!string.IsNullOrWhiteSpace(list.OrderedStart) && int.TryParse(list.OrderedStart, out var parsed))
            {
                return parsed;
            }

            return 1;
        }
    }

    private sealed class MarkdownImageControl(MarkdownImageLoader loader) : Image
    {
        public static readonly StyledProperty<string?> SourceUrlProperty =
            AvaloniaProperty.Register<MarkdownImageControl, string?>(nameof(SourceUrl));

        public string? SourceUrl
        {
            get => GetValue(SourceUrlProperty);
            set => SetValue(SourceUrlProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SourceUrlProperty)
            {
                LoadAsync();
            }
        }

        private async void LoadAsync()
        {
            try
            {
                Source = SourceUrl is null ? null : await loader.LoadImageAsync(SourceUrl);
            }
            catch
            {
                Source = null;
            }
        }
    }

    private sealed class AnonymousDisposable(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }
}