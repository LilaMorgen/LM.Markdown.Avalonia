using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Reflection;

namespace LM.Markdown.Avalonia.Controls;

/// <summary>
/// Manages cross-block text selection in MarkdownViewer.
/// Intercepts pointer events at the ScrollViewer level to enable
/// selecting text across multiple SelectableTextBlock controls,
/// with auto-scrolling when dragging near the viewport edges.
/// </summary>
internal sealed class CrossBlockSelectionHandler
{
    private readonly Panel _contentPanel;
    private readonly ScrollViewer _scrollViewer;

    private bool _isPointerPressed;
    private bool _isCrossBlockMode;
    private Control? _anchorElement;
    private int _anchorCharIndex;
    private List<Control> _orderedElements = [];

    // --- Source mapping for markdown-source copy ---
    private string _markdownSource = string.Empty;
    private IReadOnlyDictionary<Control, (int Start, int End)> _sourceSpanMap =
        new Dictionary<Control, (int Start, int End)>();

    // --- Auto-scroll state ---
    private DispatcherTimer? _autoScrollTimer;
    private double _autoScrollSpeed;          // pixels per tick; negative = up, positive = down
    private Point _lastPointerPositionInPanel; // last known pointer pos in content-panel coordinates

    // Edge zone: pointer within this many pixels of viewport top/bottom triggers auto-scroll.
    private const double EdgeZoneSize = 40;
    // Base scroll speed (pixels per tick) at the edge boundary.
    private const double BaseScrollSpeed = 8;
    // Maximum scroll speed cap.
    private const double MaxScrollSpeed = 60;
    // Timer interval (~60 fps).
    private const int AutoScrollIntervalMs = 16;

    private static readonly PropertyInfo? TextLayoutProperty =
        typeof(TextBlock).GetProperty("TextLayout",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    /// <summary>CSS class applied to non-text block controls that should participate in selection.</summary>
    internal const string SelectableBlockClass = "selectable-block";

    /// <summary>CSS class applied to the visual overlay used by selectable non-text blocks.</summary>
    internal const string SelectableBlockOverlayClass = "selectable-block-overlay";

    /// <summary>
    /// Marks SelectableTextBlock instances that are inside the current cross-block
    /// selection range even when Avalonia reports no text-range selection, such as
    /// blocks containing only InlineUIContainer math formulas.
    /// </summary>
    private const string InlineContainerSelectionFallbackClass = "inline-container-selection-fallback";

    /// <summary>Cached brush used for non-text block selection overlay.</summary>
    private IBrush? _cachedSelectionBrush;

    /// <summary>Cached semi-transparent brush used by foreground overlays.</summary>
    private IBrush? _cachedSelectionOverlayBrush;

    public CrossBlockSelectionHandler(Panel contentPanel, ScrollViewer scrollViewer)
    {
        _contentPanel = contentPanel;
        _scrollViewer = scrollViewer;

        // Tunnel handlers fire before children process the event
        _scrollViewer.AddHandler(
            InputElement.PointerPressedEvent,
            OnPointerPressed,
            RoutingStrategies.Tunnel);

        // Use handledEventsToo because SelectableTextBlock marks events as handled
        _scrollViewer.AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        _scrollViewer.AddHandler(
            InputElement.PointerReleasedEvent,
            OnPointerReleased,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        _scrollViewer.AddHandler(
            InputElement.KeyDownEvent,
            OnKeyDown,
            RoutingStrategies.Tunnel);

        // Wheel handler to allow scrolling while selecting
        _scrollViewer.AddHandler(
            InputElement.PointerWheelChangedEvent,
            OnPointerWheelChanged,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        // Make scrollviewer focusable so it can receive keyboard events
        _scrollViewer.Focusable = true;
    }

    public void Detach()
    {
        StopAutoScroll();
        _orderedElements.Clear();
        _anchorElement = null;
        _markdownSource = string.Empty;
        _sourceSpanMap = new Dictionary<Control, (int Start, int End)>();
        _isPointerPressed = false;
        _isCrossBlockMode = false;
        _scrollViewer.RemoveHandler(InputElement.PointerPressedEvent, (EventHandler<PointerPressedEventArgs>)OnPointerPressed);
        _scrollViewer.RemoveHandler(InputElement.PointerMovedEvent, (EventHandler<PointerEventArgs>)OnPointerMoved);
        _scrollViewer.RemoveHandler(InputElement.PointerReleasedEvent, (EventHandler<PointerReleasedEventArgs>)OnPointerReleased);
        _scrollViewer.RemoveHandler(InputElement.KeyDownEvent, (EventHandler<KeyEventArgs>)OnKeyDown);
        _scrollViewer.RemoveHandler(InputElement.PointerWheelChangedEvent, (EventHandler<PointerWheelEventArgs>)OnPointerWheelChanged);
    }

    /// <summary>
    /// Called by MarkdownViewer after each render to provide the original markdown text
    /// and the mapping from rendered controls to their source character ranges.
    /// </summary>
    public void UpdateSourceMapping(string markdownSource, IReadOnlyDictionary<Control, (int Start, int End)> sourceSpanMap)
    {
        _markdownSource = markdownSource ?? string.Empty;
        _sourceSpanMap = sourceSpanMap ?? new Dictionary<Control, (int Start, int End)>();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_scrollViewer).Properties.IsLeftButtonPressed)
            return;

        // Ignore clicks on scrollbars — they should scroll, not start text selection
        if (IsScrollBarInteraction(e))
            return;

        StopAutoScroll();

        // Refresh the ordered list of all selectable blocks
        _orderedElements = CollectSelectableElements(_contentPanel);
        _cachedSelectionBrush = null; // invalidate per-session cache
        _cachedSelectionOverlayBrush = null;

        // Clear previous cross-block selections
        ClearAllSelections();

        _isPointerPressed = true;
        _isCrossBlockMode = false;

        var pointInPanel = e.GetPosition(_contentPanel);
        _anchorElement = FindElementAtPoint(pointInPanel);
        _anchorCharIndex = -1;

        if (_anchorElement is SelectableTextBlock)
        {
            // Let SelectableTextBlock process the press for single-block selection.
        }
        else if (_anchorElement != null)
        {
            // Non-text block: prevent its child from capturing the pointer
            // so we receive subsequent PointerMoved events for drag selection.
            _anchorCharIndex = 0;
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerPressed || _anchorElement == null)
            return;

        var pointInPanel = e.GetPosition(_contentPanel);
        var pointInScrollViewer = e.GetPosition(_scrollViewer);
        var currentElement = FindElementAtPoint(pointInPanel) ?? FindNearestElement(pointInPanel);

        if (currentElement == null)
            return;

        // Determine whether the pointer is in the viewport edge zone (needs auto-scroll)
        bool inEdgeZone = IsInEdgeZone(pointInScrollViewer);

        if (!_isCrossBlockMode)
        {
            if (_anchorElement is SelectableTextBlock anchorStb)
            {
                // Text block anchor: enter cross-block mode when moving to a
                // different element or reaching the viewport edge zone.
                if (currentElement == _anchorElement && !inEdgeZone)
                    return;

                _isCrossBlockMode = true;
                _anchorCharIndex = anchorStb.SelectionStart;
                e.Pointer.Capture(_scrollViewer);
                _scrollViewer.Focus();
            }
            else
            {
                // Non-text block anchor: enter cross-block mode on any drag.
                _isCrossBlockMode = true;
                _anchorCharIndex = 0;
                e.Pointer.Capture(_scrollViewer);
                _scrollViewer.Focus();
            }
        }

        // In cross-block mode: manage selections and auto-scroll ourselves
        _lastPointerPositionInPanel = pointInPanel;
        UpdateSelections(currentElement, pointInPanel);
        UpdateAutoScroll(pointInScrollViewer);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        StopAutoScroll();

        if (_isCrossBlockMode)
        {
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        _isPointerPressed = false;
        // Keep _isCrossBlockMode true so Ctrl+C can still copy the selection
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!_isPointerPressed || _anchorElement == null)
            return;

        // Enter cross-block mode if not already (wheel scroll during single-block selection)
        if (!_isCrossBlockMode)
        {
            _isCrossBlockMode = true;
            _anchorCharIndex = _anchorElement is SelectableTextBlock stb ? stb.SelectionStart : 0;
            e.Pointer.Capture(_scrollViewer);
            _scrollViewer.Focus();

            // Initialize the last pointer position from current anchor element
            var anchorTop = _anchorElement.TranslatePoint(new Point(0, 0), _contentPanel);
            if (anchorTop.HasValue)
            {
                _lastPointerPositionInPanel = new Point(
                    anchorTop.Value.X,
                    anchorTop.Value.Y + _anchorElement.Bounds.Height / 2);
            }
        }

        // Scroll amount: typically Delta.Y is +1 (up) or -1 (down) per notch
        const double pixelsPerLine = 48;
        double scrollDelta = -e.Delta.Y * pixelsPerLine;

        double oldOffset = _scrollViewer.Offset.Y;
        double maxOffset = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        double newOffset = Math.Clamp(oldOffset + scrollDelta, 0, maxOffset);

        double actualDelta = newOffset - oldOffset;
        if (Math.Abs(actualDelta) < 0.5)
            return;

        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, newOffset);

        // Adjust the virtual pointer position — content shifted under the stationary pointer
        _lastPointerPositionInPanel = new Point(
            _lastPointerPositionInPanel.X,
            _lastPointerPositionInPanel.Y + actualDelta);

        var currentElement = FindElementAtPoint(_lastPointerPositionInPanel)
                          ?? FindNearestElement(_lastPointerPositionInPanel);
        if (currentElement != null)
        {
            UpdateSelections(currentElement, _lastPointerPositionInPanel);
        }

        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var mod = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

        if (e.Key == Key.C && e.KeyModifiers.HasFlag(mod))
        {
            // Refresh in case the document was re-rendered since last pointer press
            _orderedElements = CollectSelectableElements(_contentPanel);
            var text = GetSelectedMarkdownSource();
            if (!string.IsNullOrEmpty(text))
            {
                _ = CopyTextAsync(text);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.A && e.KeyModifiers.HasFlag(mod))
        {
            _orderedElements = CollectSelectableElements(_contentPanel);
            foreach (var element in _orderedElements)
            {
                if (element is SelectableTextBlock stb)
                    stb.SelectAll();
                else
                    SetNonTextBlockSelected(element, true);
            }

            // Apply selection highlights to InlineUIContainer elements
            // (e.g. inline math formulas) inside the selected text blocks.
            UpdateInlineContainerHighlights();

            _isCrossBlockMode = true;
            e.Handled = true;
        }
    }

    #region Auto-Scroll

    /// <summary>
    /// Returns true if the pointer (in scroll-viewer coordinates) is within the top or bottom edge zone.
    /// </summary>
    private bool IsInEdgeZone(Point pointInScrollViewer)
    {
        double y = pointInScrollViewer.Y;
        double viewportHeight = _scrollViewer.Viewport.Height;
        return y < EdgeZoneSize || y > viewportHeight - EdgeZoneSize;
    }

    /// <summary>
    /// Starts, adjusts, or stops auto-scrolling based on pointer proximity to viewport edges.
    /// </summary>
    private void UpdateAutoScroll(Point pointerInScrollViewer)
    {
        double viewportHeight = _scrollViewer.Viewport.Height;
        double y = pointerInScrollViewer.Y;

        if (y < EdgeZoneSize)
        {
            // Near/above top edge → scroll up
            double distance = EdgeZoneSize - y;
            _autoScrollSpeed = -CalculateScrollSpeed(distance);
            EnsureAutoScrollRunning();
        }
        else if (y > viewportHeight - EdgeZoneSize)
        {
            // Near/below bottom edge → scroll down
            double distance = y - (viewportHeight - EdgeZoneSize);
            _autoScrollSpeed = CalculateScrollSpeed(distance);
            EnsureAutoScrollRunning();
        }
        else
        {
            // Inside safe zone — stop auto-scrolling
            StopAutoScroll();
        }
    }

    private static double CalculateScrollSpeed(double distanceIntoEdge)
    {
        // Speed ramps linearly within the edge zone, and can exceed it when pointer is outside viewport
        double ratio = Math.Clamp(distanceIntoEdge / EdgeZoneSize, 0, 1);
        double extra = Math.Max(0, (distanceIntoEdge - EdgeZoneSize) / EdgeZoneSize);
        double speed = BaseScrollSpeed + (MaxScrollSpeed - BaseScrollSpeed) * ratio + extra * MaxScrollSpeed;
        return Math.Min(speed, MaxScrollSpeed);
    }

    private void EnsureAutoScrollRunning()
    {
        if (_autoScrollTimer != null)
            return;

        _autoScrollTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(AutoScrollIntervalMs),
        };
        _autoScrollTimer.Tick += OnAutoScrollTick;
        _autoScrollTimer.Start();
    }

    private void StopAutoScroll()
    {
        if (_autoScrollTimer == null)
            return;

        _autoScrollTimer.Stop();
        _autoScrollTimer.Tick -= OnAutoScrollTick;
        _autoScrollTimer = null;
        _autoScrollSpeed = 0;
    }

    /// <summary>
    /// Timer tick: scroll the viewport, then re-compute the effective pointer position
    /// (the content has shifted but the physical pointer hasn't moved) and update selections.
    /// </summary>
    private void OnAutoScrollTick(object? sender, EventArgs e)
    {
        if (!_isPointerPressed || !_isCrossBlockMode || _anchorElement == null)
        {
            StopAutoScroll();
            return;
        }

        // Apply scroll
        double oldOffset = _scrollViewer.Offset.Y;
        double maxOffset = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        double newOffset = Math.Clamp(oldOffset + _autoScrollSpeed, 0, maxOffset);

        if (Math.Abs(newOffset - oldOffset) < 0.5)
        {
            // Nothing to scroll (already at the boundary)
            return;
        }

        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, newOffset);

        // The actual number of pixels we scrolled
        double actualDelta = newOffset - oldOffset;

        // Adjust the "virtual" pointer position in panel coordinates.
        // The pointer hasn't physically moved, but the panel has scrolled under it,
        // so the effective panel-coordinate Y shifts by the scroll delta.
        _lastPointerPositionInPanel = new Point(
            _lastPointerPositionInPanel.X,
            _lastPointerPositionInPanel.Y + actualDelta);

        var currentElement = FindElementAtPoint(_lastPointerPositionInPanel)
                          ?? FindNearestElement(_lastPointerPositionInPanel);
        if (currentElement != null)
        {
            UpdateSelections(currentElement, _lastPointerPositionInPanel);
        }
    }

    #endregion

    #region Selection Logic

    private void UpdateSelections(Control currentElement, Point pointInPanel)
    {
        int anchorIdx = _orderedElements.IndexOf(_anchorElement!);
        int currentIdx = _orderedElements.IndexOf(currentElement);

        if (anchorIdx < 0 || currentIdx < 0)
            return;

        int startIdx = Math.Min(anchorIdx, currentIdx);
        int endIdx = Math.Max(anchorIdx, currentIdx);
        bool forward = currentIdx >= anchorIdx;

        for (int i = 0; i < _orderedElements.Count; i++)
        {
            var element = _orderedElements[i];

            if (i < startIdx || i > endIdx)
            {
                // Outside selection range - clear
                ClearElementSelection(element);
                continue;
            }

            // --- Non-text selectable block (e.g. block math formula) ---
            if (element is not SelectableTextBlock block)
            {
                // Always fully selected when inside the range
                SetNonTextBlockSelected(element, true);
                continue;
            }

            // --- SelectableTextBlock logic (unchanged) ---
            if (i == anchorIdx && i == currentIdx)
            {
                // Both anchor and extent in same text block
                int a = Math.Max(_anchorCharIndex >= 0 ? _anchorCharIndex : 0, 0);
                int c = HitTestCharIndex(block, pointInPanel);
                block.SelectionStart = Math.Min(a, c);
                block.SelectionEnd = Math.Max(a, c);
            }
            else if (i == anchorIdx)
            {
                // Anchor block: select from anchor char to end (forward) or beginning (backward)
                int a = Math.Max(_anchorCharIndex >= 0 ? _anchorCharIndex : 0, 0);
                if (forward)
                {
                    // Use SelectAll to get the correct end position from Avalonia,
                    // then adjust SelectionStart. This avoids text-length mismatches
                    // that caused int.MaxValue to break SelectedText.
                    block.SelectAll();
                    block.SelectionStart = a;
                }
                else
                {
                    block.SelectionStart = 0;
                    block.SelectionEnd = a;
                }
            }
            else if (i == currentIdx)
            {
                // Extent block: select from beginning (forward) or end (backward) to hit position
                int c = HitTestCharIndex(block, pointInPanel);
                if (forward)
                {
                    block.SelectionStart = 0;
                    block.SelectionEnd = c;
                }
                else
                {
                    block.SelectAll();
                    block.SelectionStart = c;
                }
            }
            else
            {
                // Middle block: select all — delegates correct text length to Avalonia
                block.SelectAll();
            }

            SetInlineContainerSelectionFallback(block, true);
        }

        // Update visual highlights on InlineUIContainer elements (e.g., math formulas)
        UpdateInlineContainerHighlights();
    }

    /// <summary>
    /// Walks the Inlines of every SelectableTextBlock and toggles the
    /// Background of any Border child inside an InlineUIContainer based on
    /// whether the container's character position falls within the block's
    /// current selection range.  This provides visual selection feedback
    /// for non-text inline elements like math formulas.
    /// </summary>
    private void UpdateInlineContainerHighlights()
    {
        foreach (var element in _orderedElements)
        {
            if (element is not SelectableTextBlock block || block.Inlines is not { Count: > 0 })
                continue;

            int selStart = block.SelectionStart;
            int selEnd = block.SelectionEnd;
            bool hasSelection = selEnd > selStart;
            bool selectAllInlineContainers = !hasSelection && HasInlineContainerSelectionFallback(block);

            // Resolve the selection brush from the block (matches text selection colour)
            var selectionBrush = (hasSelection || selectAllInlineContainers) ? block.SelectionBrush : null;

            int offset = 0;
            ApplyInlineHighlights(
                block.Inlines,
                ref offset,
                selStart,
                selEnd,
                hasSelection,
                selectAllInlineContainers,
                selectionBrush);
        }
    }

    private static void ApplyInlineHighlights(
        global::Avalonia.Controls.Documents.InlineCollection inlines,
        ref int offset,
        int selStart,
        int selEnd,
        bool hasSelection,
        bool selectAllInlineContainers,
        IBrush? selectionBrush)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run r:
                    offset += r.Text?.Length ?? 0;
                    break;

                case Span s:
                    if (s.Inlines is { Count: > 0 })
                        ApplyInlineHighlights(s.Inlines, ref offset, selStart, selEnd, hasSelection, selectAllInlineContainers, selectionBrush);
                    break;

                case LineBreak:
                    offset += 1;
                    break;

                case InlineUIContainer iuc:
                    bool isSelected = selectAllInlineContainers || (hasSelection && offset >= selStart && offset < selEnd);

                    // Avalonia's SelectAll() (and programmatic SelectionEnd) may
                    // not count InlineUIContainer characters in the text length,
                    // so selEnd can equal the IUC's offset even when the block is
                    // fully selected.  When all preceding text is within the
                    // selection (offset == selEnd and the selection is non-empty),
                    // the IUC should also be considered selected.
                    if (!isSelected && hasSelection && offset == selEnd && offset > 0)
                        isSelected = true;

                    // Grid wrapper (MathInlineRenderer): the first child is
                    // the height-constrained selection-background Border.
                    if (iuc.Child is Grid grid
                        && grid.Children.Count >= 1
                        && grid.Children[0] is Border selBg)
                    {
                        selBg.Background = isSelected ? selectionBrush : null;
                    }
                    // Simple Border wrapper fallback.
                    else if (iuc.Child is Border border)
                    {
                        border.Background = isSelected ? selectionBrush : null;
                    }

                    offset += 1;
                    break;
            }
        }
    }

    #endregion

    #region Hit Testing

    private int HitTestCharIndex(SelectableTextBlock block, Point pointInPanel)
    {
        try
        {
            var localPoint = _contentPanel.TranslatePoint(pointInPanel, block);
            if (localPoint.HasValue)
            {
                var textLayout = GetTextLayout(block);
                if (textLayout != null)
                {
                    var hit = textLayout.HitTestPoint(localPoint.Value);
                    return Math.Max(0, hit.TextPosition);
                }
            }
        }
        catch
        {
            // Fallback below
        }

        // Fallback: estimate based on vertical position within the block
        return EstimateCharIndex(block, pointInPanel);
    }

    private int EstimateCharIndex(SelectableTextBlock block, Point pointInPanel)
    {
        try
        {
            var topLeft = block.TranslatePoint(new Point(0, 0), _contentPanel);
            if (topLeft.HasValue)
            {
                double relY = pointInPanel.Y - topLeft.Value.Y;
                int textLen = GetTextLength(block);

                if (relY <= 0) return 0;
                if (relY >= block.Bounds.Height) return textLen;

                // Proportional estimate
                double ratio = relY / Math.Max(1, block.Bounds.Height);
                return (int)(ratio * textLen);
            }
        }
        catch
        {
            // ignore
        }

        return GetTextLength(block);
    }

    private static TextLayout? GetTextLayout(TextBlock block)
    {
        try
        {
            return TextLayoutProperty?.GetValue(block) as TextLayout;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Block Discovery

    /// <summary>
    /// Returns true if the pointer event originated from a ScrollBar (thumb, track, etc.).
    /// Walks up the visual tree from the event source to check for a ScrollBar ancestor.
    /// </summary>
    private static bool IsScrollBarInteraction(PointerEventArgs e)
    {
        Visual? source = e.Source as Visual;
        while (source != null)
        {
            if (source is ScrollBar)
                return true;
            source = source.GetVisualParent();
        }
        return false;
    }

    private Control? FindElementAtPoint(Point pointInPanel)
    {
        foreach (var element in _orderedElements)
        {
            var topLeft = element.TranslatePoint(new Point(0, 0), _contentPanel);
            if (topLeft == null) continue;

            var bounds = new Rect(topLeft.Value, element.Bounds.Size);
            if (bounds.Contains(pointInPanel))
                return element;
        }

        return null;
    }

    private Control? FindNearestElement(Point pointInPanel)
    {
        Control? best = null;
        double bestDist = double.MaxValue;

        foreach (var element in _orderedElements)
        {
            var topLeft = element.TranslatePoint(new Point(0, 0), _contentPanel);
            if (topLeft == null) continue;

            var bounds = new Rect(topLeft.Value, element.Bounds.Size);

            double dist = 0;
            if (pointInPanel.Y < bounds.Top) dist = bounds.Top - pointInPanel.Y;
            else if (pointInPanel.Y > bounds.Bottom) dist = pointInPanel.Y - bounds.Bottom;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = element;
            }
        }

        return best;
    }

    private static List<Control> CollectSelectableElements(Visual root)
    {
        var list = new List<Control>();
        CollectRecursive(root, list);
        return list;
    }

    private static void CollectRecursive(Visual visual, List<Control> list)
    {
        // Non-text selectable block (e.g. block math formula)
        if (visual is Control ctrl && ctrl.Classes.Contains(SelectableBlockClass))
        {
            list.Add(ctrl);
            return; // don't descend – avoids double-counting nested text blocks
        }

        if (visual is SelectableTextBlock stb)
        {
            list.Add(stb);
            return;
        }

        foreach (var child in visual.GetVisualChildren())
            CollectRecursive(child, list);
    }

    #endregion

    #region Text Helpers

    /// <summary>
    /// Returns the original markdown source text for the currently selected range.
    /// <para>
    /// Fully selected blocks → their exact markdown source is extracted (preserving syntax).
    /// Partially selected edge blocks → rendered <c>SelectedText</c> is used instead,
    /// to avoid including markdown content beyond what the user actually highlighted.
    /// </para>
    /// </summary>
    public string GetSelectedMarkdownSource()
    {
        if (string.IsNullOrEmpty(_markdownSource) || _sourceSpanMap.Count == 0)
            return GetCombinedSelectedText();

        // Gather info about each element that has any selection
        var entries = new List<(Control Element, bool FullySelected, (int Start, int End)? Span, string? SelectedText)>();
        foreach (var element in _orderedElements)
        {
            if (element is SelectableTextBlock stb)
            {
                bool hasInlineSelectionFallback = HasInlineContainerSelectionFallback(stb)
                    && CountInlineUIContainers(stb.Inlines) > 0;

                if (string.IsNullOrEmpty(stb.SelectedText) && !hasInlineSelectionFallback)
                    continue;
                var span = FindTopLevelSourceSpan(stb);
                int textLen = GetTextLength(stb);
                // Avalonia's SelectAll() may not count InlineUIContainer characters
                // in SelectionEnd. Subtract IUC count so fully-selected blocks that
                // contain inline formulas are still detected correctly.
                int iucCount = CountInlineUIContainers(stb.Inlines);
                bool fully = hasInlineSelectionFallback
                    || (stb.SelectionStart <= 0 && stb.SelectionEnd >= textLen - iucCount);
                entries.Add((element, fully, span, string.IsNullOrEmpty(stb.SelectedText) ? null : stb.SelectedText));
            }
            else if (IsNonTextBlockSelected(element))
            {
                var span = FindTopLevelSourceSpan(element);
                entries.Add((element, true, span, null));
            }
        }

        if (entries.Count == 0)
            return GetCombinedSelectedText();

        // Fast path: every selected block is fully selected → extract one continuous markdown range
        if (entries.All(e => e.FullySelected && e.Span.HasValue))
        {
            int minStart = entries.Min(e => e.Span!.Value.Start);
            int maxEnd = entries.Max(e => e.Span!.Value.End);
            return ExtractSourceRange(minStart, maxEnd);
        }

        // Mixed: some blocks are partially selected (typically the first and/or last block).
        var parts = new List<string>();
        int i = 0;

        while (i < entries.Count)
        {
            var (element, fully, span, selectedText) = entries[i];

            if (!fully || !span.HasValue)
            {
                // Partially selected → rendered text only
                if (!string.IsNullOrEmpty(selectedText))
                    parts.Add(selectedText);
                i++;
                continue;
            }

            // Collect a run of consecutive fully-selected blocks
            int runStart = span.Value.Start;
            int runEnd = span.Value.End;
            i++;

            while (i < entries.Count && entries[i].FullySelected && entries[i].Span.HasValue)
            {
                runStart = Math.Min(runStart, entries[i].Span!.Value.Start);
                runEnd = Math.Max(runEnd, entries[i].Span!.Value.End);
                i++;
            }

            var chunk = ExtractSourceRange(runStart, runEnd);
            if (!string.IsNullOrEmpty(chunk))
                parts.Add(chunk);
        }

        return string.Join("\n\n", parts).TrimEnd();
    }

    private string ExtractSourceRange(int start, int end)
    {
        int safeEnd = Math.Min(end, _markdownSource.Length - 1);
        if (start >= 0 && start <= safeEnd)
            return _markdownSource[start..(safeEnd + 1)].TrimEnd();
        return string.Empty;
    }

    /// <summary>
    /// Walks up the visual tree from an element to find the
    /// outermost (top-level) ancestor that has a source span mapping.
    /// This ensures we get the full block's markdown including syntax
    /// (e.g., heading markers, fence markers, quote prefixes).
    /// </summary>
    private (int Start, int End)? FindTopLevelSourceSpan(Control element)
    {
        (int Start, int End)? topmost = null;
        Visual? current = element;

        while (current != null && current != _contentPanel)
        {
            if (current is Control control && _sourceSpanMap.TryGetValue(control, out var span))
                topmost = span;
            current = current.GetVisualParent();
        }

        return topmost;
    }

    private void ClearAllSelections()
    {
        foreach (var element in _orderedElements)
            ClearElementSelection(element);

        // Also clear any inline container highlights
        UpdateInlineContainerHighlights();
    }

    public string GetCombinedSelectedText()
    {
        var parts = new List<string>();
        foreach (var element in _orderedElements)
        {
            if (element is SelectableTextBlock stb)
            {
                var text = stb.SelectedText;
                if (!string.IsNullOrEmpty(text))
                    parts.Add(text);
            }
            // Non-text blocks don't contribute plain text.
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static int GetTextLength(SelectableTextBlock block)
    {
        if (block.Inlines is { Count: > 0 })
        {
            int total = 0;
            foreach (var inline in block.Inlines)
                total += MeasureInlineLength(inline);
            return total;
        }

        return block.Text?.Length ?? 0;
    }

    private static int MeasureInlineLength(Inline inline) => inline switch
    {
        Run r => r.Text?.Length ?? 0,
        Span s => s.Inlines.Sum(MeasureInlineLength),
        LineBreak => 1,
        InlineUIContainer => 1,
        _ => 0,
    };

    /// <summary>
    /// Recursively counts the number of <see cref="InlineUIContainer"/> elements
    /// in an inline collection.  Used to compensate for Avalonia's
    /// <c>SelectAll()</c> potentially not counting them in
    /// <c>SelectionEnd</c>.
    /// </summary>
    private static int CountInlineUIContainers(InlineCollection? inlines)
    {
        if (inlines is null or { Count: 0 })
            return 0;

        int count = 0;
        foreach (var inline in inlines)
        {
            if (inline is InlineUIContainer)
                count++;
            else if (inline is Span s)
                count += CountInlineUIContainers(s.Inlines);
        }
        return count;
    }

    private async Task CopyTextAsync(string text)
    {
        var topLevel = TopLevel.GetTopLevel(_scrollViewer);
        if (topLevel?.Clipboard != null)
            await topLevel.Clipboard.SetTextAsync(text);
    }

    #endregion

    #region Non-Text Block Selection Helpers

    /// <summary>
    /// Shows or hides the selection overlay on a non-text selectable block.
    /// Sets the Border's Background to a semi-transparent selection brush.
    /// </summary>
    private void SetNonTextBlockSelected(Control element, bool selected)
    {
        if (element is Border border)
        {
            var selectionBrush = selected ? GetNonTextBlockSelectionBrush() : null;
            var overlayBrush = selected ? GetNonTextBlockOverlayBrush() : null;
            border.Background = selectionBrush;

            if (TryGetNonTextBlockOverlay(border) is { } overlay)
                overlay.Background = overlayBrush;
        }
    }

    /// <summary>
    /// Returns true if a non-text block is currently showing a selection overlay.
    /// </summary>
    private static bool IsNonTextBlockSelected(Control element)
    {
        return element is Border border
            && border.Classes.Contains(SelectableBlockClass)
            && border.Background != null;
    }

    /// <summary>
    /// Clears the selection state on an element (text or non-text).
    /// </summary>
    private void ClearElementSelection(Control element)
    {
        if (element is SelectableTextBlock stb)
        {
            stb.SelectionStart = 0;
            stb.SelectionEnd = 0;
            SetInlineContainerSelectionFallback(stb, false);
        }
        else
        {
            SetNonTextBlockSelected(element, false);
        }
    }

    /// <summary>
    /// Returns the brush used to highlight non-text blocks during selection.
    /// Tries to match the SelectionBrush of the nearest SelectableTextBlock
    /// for visual consistency; falls back to a semi-transparent blue.
    /// </summary>
    private IBrush GetNonTextBlockSelectionBrush()
    {
        if (_cachedSelectionBrush != null)
            return _cachedSelectionBrush;

        foreach (var element in _orderedElements)
        {
            if (element is SelectableTextBlock stb && stb.SelectionBrush is IBrush brush)
            {
                _cachedSelectionBrush = CreateSelectionTintBrush(brush, 0.18);
                return _cachedSelectionBrush;
            }
        }

        _cachedSelectionBrush = new SolidColorBrush(Color.FromArgb(46, 65, 105, 225));
        return _cachedSelectionBrush;
    }

    private IBrush GetNonTextBlockOverlayBrush()
    {
        if (_cachedSelectionOverlayBrush != null)
            return _cachedSelectionOverlayBrush;

        foreach (var element in _orderedElements)
        {
            if (element is SelectableTextBlock stb && stb.SelectionBrush is IBrush brush)
            {
                _cachedSelectionOverlayBrush = CreateSelectionTintBrush(brush, 0.35);
                return _cachedSelectionOverlayBrush;
            }
        }

        _cachedSelectionOverlayBrush = new SolidColorBrush(Color.FromArgb(90, 65, 105, 225));
        return _cachedSelectionOverlayBrush;
    }

    private static IBrush CreateSelectionTintBrush(IBrush brush, double targetOpacity)
    {
        if (brush is ISolidColorBrush solidBrush)
        {
            var effectiveAlpha = (byte)Math.Clamp(
                255 * solidBrush.Opacity * targetOpacity,
                0,
                255);

            return new SolidColorBrush(
                Color.FromArgb(effectiveAlpha, solidBrush.Color.R, solidBrush.Color.G, solidBrush.Color.B));
        }

        return new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(255 * targetOpacity, 0, 255), 65, 105, 225));
    }

    private static Border? TryGetNonTextBlockOverlay(Border border)
    {
        if (border.Child is not Grid grid)
            return null;

        foreach (var child in grid.Children)
        {
            if (child is Border overlay && overlay.Classes.Contains(SelectableBlockOverlayClass))
                return overlay;
        }

        return null;
    }

    private static void SetInlineContainerSelectionFallback(SelectableTextBlock block, bool enabled)
    {
        if (enabled)
            block.Classes.Add(InlineContainerSelectionFallbackClass);
        else
            block.Classes.Remove(InlineContainerSelectionFallbackClass);
    }

    private static bool HasInlineContainerSelectionFallback(SelectableTextBlock block)
        => block.Classes.Contains(InlineContainerSelectionFallbackClass);

    #endregion
}
