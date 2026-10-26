using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Syntax;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class HelpWindow
{
    /// <summary>What the search found last (Enter opens the first).</summary>
    internal IReadOnlyList<HelpHit> Hits { get; private set; } = [];

    /// <summary>The side: what the search finds while something is typed in it, the contents otherwise.</summary>
    private void ShowSide()
    {
        Side.Children.Clear();
        string query = SearchBox.Text ?? "";
        if (HelpSearch.Words(query).Length > 0)
        {
            ShowHits(query);
        }
        else
        {
            Hits = [];
            ShowContents();
        }
    }

    /// <summary>
    /// The contents: each part's heading, its pages; the page shown is marked, with its own parts (its "##" headings)
    /// under it. A page shown that the contents do not list (one a link led to) is at the top.
    /// </summary>
    private void ShowContents()
    {
        if (HelpContents.Find(Shown.Page) == null && Shown.Page != HelpDocs.Home)
        {
            AddEntry(new HelpEntry(_title.Text ?? Shown.Page, new HelpTarget(Shown.Page)));
        }
        foreach (var section in HelpContents.Sections)
        {
            Side.Children.Add(Ui.Heading(section.Title, 12));
            foreach (var entry in section.Entries)
            {
                AddEntry(entry);
            }
        }
    }

    /// <summary>A page of the contents (marked when shown, with its parts under it then).</summary>
    private void AddEntry(HelpEntry entry)
    {
        bool shown = entry.Target.Page == Shown.Page;
        Side.Children.Add(SideButton(new TextBlock { Text = entry.Title, TextWrapping = TextWrapping.Wrap }, new HelpTarget(entry.Target.Page), shown));
        if (shown && View != null)
        {
            foreach (var (title, id) in Parts())
            {
                var t = new TextBlock { Text = title, FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
                var b = SideButton(t, new HelpTarget(Shown.Page, id), Shown.Anchor == id);
                b.Margin = new Thickness(14, 0, 0, 0);
                Side.Children.Add(b);
            }
        }
    }

    /// <summary>The page shown's parts: each "##" heading's text and id.</summary>
    private IEnumerable<(string Title, string Id)> Parts() =>
        (_doc?.OfType<HeadingBlock>() ?? [])
            .Where(h => h.Level == 2 && HelpText.Id(h) != null)
            .Select(h => (HelpText.Of(h.Inline), HelpText.Id(h)!));

    /// <summary>What a search found: each part's page, heading and a few of its words; or that nothing was.</summary>
    private void ShowHits(string query)
    {
        Hits = HelpSearch.Carried.Find(query);
        if (Hits.Count == 0)
        {
            Side.Children.Add(new TextBlock { Text = Lang.T("docs.noHits", query.Trim()), Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 8) });
            return;
        }
        foreach (var hit in Hits)
        {
            var lines = new StackPanel { Spacing = 2 };
            lines.Children.Add(new TextBlock { Text = hit.Page, FontSize = 11, Foreground = Ui.Muted });
            lines.Children.Add(new TextBlock { Text = hit.Heading, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            lines.Children.Add(new TextBlock { Text = hit.Snippet, FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap });
            var b = SideButton(lines, hit.Target, false);
            b.Padding = new Thickness(8, 6);
            Side.Children.Add(b);
        }
    }

    /// <summary>Opens what the search found first, if anything.</summary>
    internal void OpenFirstHit()
    {
        if (Hits.Count > 0)
        {
            Go(Hits[0].Target);
        }
    }

    /// <summary>A flat button of the side, going to a place; marked ("on") when it is the one shown.</summary>
    private Button SideButton(Control content, HelpTarget target, bool on)
    {
        var b = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 4),
        }.Classed("ghost");
        b.Classes.Set("on", on);
        b.Click += (_, _) => Go(target);
        return b;
    }
}
