using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// A documentation page drawn with the app's controls, in its colours: headings, paragraphs, lists, tables, pictures
/// (a click shows one full size), tips and warnings ("> [!TIP]"), code. Links are followed through <c>Follow</c>; the
/// headings are kept by id, so the window can scroll to one (<c>Anchors</c>).
/// </summary>
public sealed partial class HelpView
{
    /// <summary>The text's size, and the space between its lines.</summary>
    private const double BodySize = 14, BodyLine = 21;

    /// <summary>The page drawn, under docs/ (where its links and pictures start from).</summary>
    private readonly string _page;

    /// <summary>What a click on a link does, with the link as written.</summary>
    private readonly Action<string> _follow;

    /// <summary>What a click on a picture does, with its path under docs/.</summary>
    private readonly Action<string> _enlarge;

    /// <summary>Each heading by its id ("walk-and-fly"), to scroll to.</summary>
    public Dictionary<string, Control> Anchors { get; } = [];

    /// <summary>Sets up the drawing of a page.</summary>
    /// <param name="page">The page, under docs/.</param>
    /// <param name="follow">What a click on a link does, with the link as written.</param>
    /// <param name="enlarge">What a click on a picture does, with its path under docs/.</param>
    public HelpView(string page, Action<string> follow, Action<string> enlarge)
    {
        _page = page;
        _follow = follow;
        _enlarge = enlarge;
    }

    /// <summary>The page drawn: its blocks one under the other.</summary>
    /// <param name="doc">The page, read.</param>
    public StackPanel Build(MarkdownDocument doc) => Blocks(doc, 12);

    /// <summary>A container's blocks, one under the other.</summary>
    /// <param name="blocks">The container (the page, a list item, a quote...).</param>
    /// <param name="spacing">The space between them.</param>
    private StackPanel Blocks(ContainerBlock blocks, double spacing)
    {
        var panel = new StackPanel { Spacing = spacing };
        Block? before = null;
        foreach (var block in blocks)
        {
            if (Draw(block, before) is { } c)
            {
                panel.Children.Add(c);
            }
            before = block;
        }
        return panel;
    }

    /// <summary>One block drawn; null for what is not shown (HTML, link definitions).</summary>
    /// <param name="block">The block.</param>
    /// <param name="before">The block above it (a picture's caption is the line in italics under it).</param>
    private Control? Draw(Block block, Block? before) => block switch
    {
        HeadingBlock h => Heading(h),
        ParagraphBlock { Inline: { } inline } when LeadingPicture(inline) is { } pic => PictureAndCaption(pic),
        ParagraphBlock { Inline: { } inline } when IsCaption(inline, before) => Caption(inline),
        ParagraphBlock p => Paragraph(p.Inline),
        ListBlock l => List(l),
        Table t => Table(t),
        AlertBlock a => Alert(a),
        QuoteBlock q => Quote(q),
        CodeBlock c => Code(c),
        ThematicBreakBlock => new Border { Height = 1, Background = Ui.Line, Margin = new Thickness(0, 6) },
        _ => null,
    };

    /// <summary>A heading, larger the higher its level; kept by id. Level 2 has a line under it.</summary>
    private Control Heading(HeadingBlock h)
    {
        var (size, top) = h.Level switch { 1 => (26.0, 0.0), 2 => (20.0, 14.0), 3 => (16.5, 8.0), _ => (14.5, 4.0) };
        var text = Text(h.Inline, size, size * 1.3);
        text.FontWeight = h.Level <= 2 ? FontWeight.SemiBold : FontWeight.Bold;
        Control drawn = text;
        if (h.Level == 2)
        {
            drawn = new Border { Child = text, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 6) };
        }
        drawn.Margin = new Thickness(0, top, 0, 0);
        if (HelpText.Id(h) is { } id)
        {
            Anchors.TryAdd(id, drawn);
        }
        return drawn;
    }

    /// <summary>A paragraph's text.</summary>
    private TextBlock Paragraph(ContainerInline? inline) => Text(inline, BodySize, BodyLine);

    /// <summary>A picture's caption: the paragraph in italics right under it, smaller and grey.</summary>
    private TextBlock Caption(ContainerInline inline)
    {
        var t = Text(inline, 12.5, 18);
        t.Foreground = Ui.Muted;
        t.Margin = new Thickness(0, -6, 0, 0);
        return t;
    }

    /// <summary>
    /// The picture a paragraph starts with, on a line of its own (alone, or with its caption on the next lines); null
    /// for a paragraph of text.
    /// </summary>
    private static LinkInline? LeadingPicture(ContainerInline inline) =>
        inline.FirstChild is LinkInline { IsImage: true } pic && pic.NextSibling is null or LineBreakInline ? pic : null;

    /// <summary>Whether a paragraph is a caption: in italics, right under a picture's paragraph.</summary>
    private static bool IsCaption(ContainerInline inline, Block? before) =>
        before is ParagraphBlock { Inline: { } b } && LeadingPicture(b) != null && inline.FirstChild is EmphasisInline;

    /// <summary>A picture, with the lines under it in its paragraph as its caption.</summary>
    private Control PictureAndCaption(LinkInline pic)
    {
        var picture = Picture(pic);
        if (pic.NextSibling?.NextSibling is not { } caption)
        {
            return picture;
        }
        var t = Text(pic.Parent, 12.5, 18, caption);
        t.Foreground = Ui.Muted;
        return new StackPanel { Spacing = 6, Children = { picture, t } };
    }

    /// <summary>
    /// A picture at the page's width (never wider than it is), its description as its tip; a click shows it full size.
    /// One that is not carried shows its description instead.
    /// </summary>
    private Control Picture(LinkInline pic)
    {
        string alt = HelpText.Of(pic);
        string path = HelpDocs.Combine(_page, pic.Url ?? "");
        using var stream = HelpDocs.Open(path);
        if (stream == null)
        {
            return new TextBlock { Text = "[" + alt + "]", Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
        }
        var bitmap = new Bitmap(stream);
        var image = new Image { Source = bitmap, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = bitmap.Size.Width };
        var frame = new Border
        {
            Child = image,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        ToolTip.SetTip(frame, alt);
        Avalonia.Automation.AutomationProperties.SetName(image, alt);
        frame.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left)
            {
                _enlarge(path);
            }
        };
        return frame;
    }

    /// <summary>A list: each item's mark (a bullet, or its number) beside its blocks.</summary>
    private StackPanel List(ListBlock list)
    {
        var panel = new StackPanel { Spacing = 4 };
        int number = list.IsOrdered && int.TryParse(list.OrderedStart, out int start) ? start : 1;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("22,*") };
            row.Children.Add(new TextBlock
            {
                Text = list.IsOrdered ? number++ + "." : "•",
                FontSize = BodySize,
                LineHeight = BodyLine,
                Foreground = list.IsOrdered ? Ui.Accent : Ui.Muted,
                HorizontalAlignment = HorizontalAlignment.Left,
            });
            row.Children.Add(Ui.Col(Blocks(item, 4), 1));
            panel.Children.Add(row);
        }
        return panel;
    }

    /// <summary>A quotation: its blocks beside a grey bar.</summary>
    private Border Quote(QuoteBlock quote) => new()
    {
        Child = Blocks(quote, 8),
        BorderBrush = Ui.Line2,
        BorderThickness = new Thickness(3, 0, 0, 0),
        Padding = new Thickness(12, 2, 0, 2),
    };

    /// <summary>A code block: its lines as written, in a fixed-width font, scrolled sideways when wider than the page.</summary>
    private static Border Code(CodeBlock code) => new()
    {
        Background = new SolidColorBrush(Color.Parse("#0f1216")),
        BorderBrush = Ui.Line,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(12, 9),
        Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new SelectableTextBlock { Text = code.Lines.ToString().TrimEnd(), FontFamily = new FontFamily("monospace"), FontSize = 12.5 },
        },
    };

    /// <summary>
    /// A tip, note or warning ("> [!TIP]"): its blocks under its kind's name, in a box edged in its kind's colour (a
    /// warning amber, a caution red).
    /// </summary>
    private Border Alert(AlertBlock alert)
    {
        string kind = alert.Kind.ToString().ToUpperInvariant();
        var (key, colour) = kind switch
        {
            "TIP" => ("docs.alert.tip", Ui.OkColor),
            "IMPORTANT" => ("docs.alert.important", Color.Parse("#a98af0")),
            "WARNING" => ("docs.alert.warning", Ui.AccentColor),
            "CAUTION" => ("docs.alert.caution", Ui.WarnColor),
            _ => ("docs.alert.note", Color.Parse("#6aa5e8")),
        };
        var body = Blocks(alert, 6);
        body.Children.Insert(0, new TextBlock { Text = Lang.T(key), FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(colour), FontSize = 13 });
        return new Border
        {
            Child = body,
            Background = new SolidColorBrush(colour, 0.08),
            BorderBrush = new SolidColorBrush(colour),
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(0, 6, 6, 0),
            Padding = new Thickness(12, 8),
        };
    }
}
