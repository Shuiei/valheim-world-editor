using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Markdig.Syntax.Inlines;

namespace TerrainEditor.Desktop;

public sealed partial class HelpView
{
    /// <summary>A link in a text block: the characters it covers (from Start, Length long) and the link as written.</summary>
    private sealed record LinkSpan(int Start, int Length, string Href);

    /// <summary>A pointer over links: the hand, the arrow elsewhere.</summary>
    private static readonly Cursor Hand = new(StandardCursorType.Hand);

    /// <summary>The size of code within the text being drawn: a little smaller than the text around it.</summary>
    private double _codeSize = BodySize;

    /// <summary>Code within text: a fixed-width font on a darker background.</summary>
    private static readonly FontFamily Mono = new("monospace");

    /// <summary>
    /// An inline run as a wrapped text block: bold, italics, code and links (in the accent colour, underlined; a click
    /// follows one, the pointer is a hand over them).
    /// </summary>
    /// <param name="inline">The run (a paragraph's, a heading's, a cell's).</param>
    /// <param name="size">The font size.</param>
    /// <param name="line">The space between lines.</param>
    /// <param name="from">The first inline drawn (a caption after its picture); null for all of them.</param>
    private TextBlock Text(ContainerInline? inline, double size, double line, Markdig.Syntax.Inlines.Inline? from = null)
    {
        var t = new TextBlock { FontSize = size, LineHeight = line, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Text };
        t.Inlines ??= [];
        var links = new List<LinkSpan>();
        int at = 0;
        _codeSize = size * 0.9;
        for (var i = from ?? inline?.FirstChild; i != null; i = i.NextSibling)
        {
            AddInline(t.Inlines, i, links, ref at);
        }

        if (links.Count > 0)
        {
            Clickable(t, links);
        }
        return t;
    }

    /// <summary>
    /// Adds an inline's drawing to a run, counting the characters it adds (<paramref name="at"/>) so that a link's are
    /// known where a click falls.
    /// </summary>
    private void AddInline(InlineCollection into, Markdig.Syntax.Inlines.Inline inline, List<LinkSpan> links, ref int at)
    {
        switch (inline)
        {
            case LiteralInline l:
                Add(into, new Run(l.Content.ToString()), ref at);
                break;
            case CodeInline c:
                Add(into, new Run(c.Content) { FontFamily = Mono, Background = Ui.Panel2, FontSize = _codeSize }, ref at);
                break;
            case LineBreakInline br:
                Add(into, new Run(br.IsHard ? "\n" : " "), ref at);
                break;
            case EmphasisInline e:
                Emphasis(into, e, links, ref at);
                break;
            case LinkInline { IsImage: true } pic:
                // A picture within text: its description, as a picture this small would say nothing.
                Add(into, new Run(HelpText.Of(pic)) { Foreground = Ui.Muted }, ref at);
                break;
            case LinkInline link:
                Link(into, link, link.Url ?? "", links, ref at);
                break;
            case AutolinkInline auto:
                AutoLink(into, auto, links, ref at);
                break;
            case ContainerInline container:
                Children(into, container, links, ref at);
                break;
            default:
                // HTML within text: not shown.
                break;
        }
    }

    /// <summary>Adds text in bold ("**"), in italics ("*") or both ("***").</summary>
    private void Emphasis(InlineCollection into, EmphasisInline e, List<LinkSpan> links, ref int at)
    {
        var span = new Span
        {
            FontWeight = e.DelimiterCount >= 2 ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = e.DelimiterCount % 2 != 0 ? FontStyle.Italic : FontStyle.Normal,
        };
        Children(span.Inlines, e, links, ref at);
        into.Add(span);
    }

    /// <summary>Adds an address written as a link ("&lt;https://...&gt;"): itself, as a link.</summary>
    private static void AutoLink(InlineCollection into, AutolinkInline auto, List<LinkSpan> links, ref int at)
    {
        int start = at;
        Add(into, LinkRun(auto.Url), ref at);
        links.Add(new LinkSpan(start, at - start, auto.IsEmail ? "mailto:" + auto.Url : auto.Url));
    }

    /// <summary>Adds a container's inlines one after another.</summary>
    private void Children(InlineCollection into, ContainerInline container, List<LinkSpan> links, ref int at)
    {
        foreach (var child in container)
        {
            AddInline(into, child, links, ref at);
        }
    }

    /// <summary>Adds a link: its text in the accent colour, underlined, and where its characters are.</summary>
    private void Link(InlineCollection into, ContainerInline link, string href, List<LinkSpan> links, ref int at)
    {
        int start = at;
        var span = new Span { Foreground = Ui.Accent, TextDecorations = TextDecorations.Underline };
        Children(span.Inlines, link, links, ref at);
        into.Add(span);
        links.Add(new LinkSpan(start, at - start, href));
    }

    /// <summary>A link's text when it has no other: the address itself.</summary>
    private static Run LinkRun(string text) => new(text) { Foreground = Ui.Accent, TextDecorations = TextDecorations.Underline };

    /// <summary>Adds a run, counting its characters.</summary>
    private static void Add(InlineCollection into, Run run, ref int at)
    {
        into.Add(run);
        at += run.Text?.Length ?? 0;
    }

    /// <summary>Each text block's links, where they are in its text.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBlock, List<LinkSpan>> LinksOf = [];

    /// <summary>Makes a text block's links work: the hand over them, a left click follows one.</summary>
    private void Clickable(TextBlock t, List<LinkSpan> links)
    {
        LinksOf.AddOrUpdate(t, links);
        t.PointerMoved += (_, e) => t.Cursor = LinkAt(t, e.GetPosition(t)) != null ? Hand : null;
        t.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left && LinkAt(t, e.GetPosition(t)) is { } href)
            {
                e.Handled = true;
                _follow(href);
            }
        };
    }

    /// <summary>The link (as written) under a point of a drawn text block; null where there is none.</summary>
    /// <param name="t">The text block.</param>
    /// <param name="p">The point, in the text block's own coordinates.</param>
    internal static string? LinkAt(TextBlock t, Avalonia.Point p)
    {
        var hit = t.TextLayout.HitTestPoint(p);
        if (!hit.IsInside || !LinksOf.TryGetValue(t, out var links))
        {
            return null;
        }
        int index = hit.TextPosition;
        return links.FirstOrDefault(l => index >= l.Start && index < l.Start + l.Length)?.Href;
    }
}
