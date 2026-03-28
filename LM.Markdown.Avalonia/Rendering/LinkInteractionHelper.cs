using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.TextFormatting;

namespace LM.Markdown.Avalonia.Rendering;

/// <summary>
/// Enables clickable links and hand cursor on <see cref="SelectableTextBlock"/>s
/// that contain link <see cref="Span"/>s.
/// <para>
/// Links are rendered as ordinary <see cref="Span"/> inlines (preserving natural
/// text baseline alignment). This helper attaches pointer event handlers to the
/// parent <see cref="SelectableTextBlock"/> and uses <see cref="TextLayout"/>
/// hit-testing to detect when the cursor is over a link span.
/// </para>
/// </summary>
internal static class LinkInteractionHelper
{
    #region Link Data Storage

    /// <summary>
    /// Stores (url, title) for each link Span. Uses ConditionalWeakTable so entries
    /// are automatically garbage-collected when the Span is no longer referenced.
    /// </summary>
    private static readonly ConditionalWeakTable<Span, LinkInfo> _linkStore = new();

    internal sealed class LinkInfo(string url, string? title)
    {
        public string Url { get; } = url;
        public string? Title { get; } = title;
    }

    /// <summary>
    /// Marks a <see cref="Span"/> as a clickable link.
    /// Called by <see cref="Inlines.LinkInlineRenderer"/> during rendering.
    /// </summary>
    public static void MarkAsLink(Span span, string url, string? title)
        => _linkStore.AddOrUpdate(span, new LinkInfo(url, title));

    public static LinkInfo? GetLinkInfo(Span span)
        => _linkStore.TryGetValue(span, out var info) ? info : null;

    #endregion

    #region Attach Handlers

    /// <summary>
    /// Scans a <see cref="SelectableTextBlock"/>'s inlines for link spans.
    /// If any are found, attaches pointer handlers for hand cursor and click-to-navigate.
    /// </summary>
    public static void AttachIfNeeded(SelectableTextBlock textBlock, RenderContext context)
    {
        if (!ContainsLinks(textBlock.Inlines))
            return;

        var handCursor = new Cursor(StandardCursorType.Hand);
        var ibeamCursor = new Cursor(StandardCursorType.Ibeam);

        // Cursor change — use Tunnel so it fires before SelectableTextBlock's own handling.
        // Skip cursor change while a button is pressed (user is selecting text).
        textBlock.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(textBlock).Properties.IsLeftButtonPressed)
                return;

            var link = HitTestLink(textBlock, e.GetPosition(textBlock));
            textBlock.Cursor = link != null ? handCursor : ibeamCursor;
        }, RoutingStrategies.Tunnel);

        // Click — use Tunnel so it fires BEFORE SelectableTextBlock starts text selection.
        // When a link is clicked, mark the event as handled to prevent selection.
        textBlock.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(textBlock).Properties.IsLeftButtonPressed)
                return;

            var link = HitTestLink(textBlock, e.GetPosition(textBlock));
            if (link != null)
            {
                context.OnLinkClicked(link.Url, link.Title);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

    private static bool ContainsLinks(InlineCollection? inlines)
    {
        if (inlines == null) return false;
        foreach (var inline in inlines)
        {
            if (inline is Span span && _linkStore.TryGetValue(span, out _))
                return true;
        }
        return false;
    }

    #endregion

    #region Hit Testing

    /// <summary>
    /// Returns the <see cref="LinkInfo"/> if the given point (relative to the TextBlock)
    /// is over a link span, or null otherwise.
    /// </summary>
    private static LinkInfo? HitTestLink(SelectableTextBlock textBlock, Point point)
    {
        try
        {
            var textLayout = GetTextLayout(textBlock);
            if (textLayout == null || textBlock.Inlines == null)
                return null;

            // Account for padding — TextLayout coordinates start inside the padding area
            var adjusted = new Point(
                point.X - textBlock.Padding.Left,
                point.Y - textBlock.Padding.Top);

            var hit = textLayout.HitTestPoint(adjusted);
            return FindLinkAtCharPosition(textBlock.Inlines, hit.TextPosition);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Walks the inline tree to find if the character at <paramref name="charPos"/>
    /// belongs to a link <see cref="Span"/>.
    /// </summary>
    private static LinkInfo? FindLinkAtCharPosition(InlineCollection inlines, int charPos)
    {
        int offset = 0;
        foreach (var inline in inlines)
        {
            int len = MeasureInlineLength(inline);
            if (charPos >= offset && charPos < offset + len)
            {
                if (inline is Span span)
                {
                    // Check if THIS span is a link
                    if (_linkStore.TryGetValue(span, out var linkInfo))
                        return linkInfo;

                    // Not a link span itself — recurse into children
                    // (a link span could be nested inside formatting like bold)
                    if (span.Inlines.Count > 0)
                        return FindLinkAtCharPosition(span.Inlines, charPos - offset);
                }
                // Reached a non-Span leaf (Run, LineBreak, etc.) — not a link
                return null;
            }
            offset += len;
        }
        return null;
    }

    private static int MeasureInlineLength(Inline inline) => inline switch
    {
        Run r => r.Text?.Length ?? 0,
        Span s => s.Inlines.Sum(MeasureInlineLength),
        LineBreak => 1,
        InlineUIContainer => 1,
        _ => 0,
    };

    private static readonly PropertyInfo? TextLayoutProperty =
        typeof(TextBlock).GetProperty("TextLayout",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

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
}
