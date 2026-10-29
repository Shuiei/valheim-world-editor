using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TerrainEditor.Desktop;

/// <summary>A page in the contents: its name there and where it leads.</summary>
/// <param name="Title">The link's text on the first page ("The map").</param>
/// <param name="Target">The page.</param>
public sealed record HelpEntry(string Title, HelpTarget Target);

/// <summary>A part of the contents: its heading on the first page and its pages, in order.</summary>
/// <param name="Title">The heading ("Getting started").</param>
/// <param name="Entries">Its pages.</param>
public sealed record HelpSection(string Title, IReadOnlyList<HelpEntry> Entries);

/// <summary>
/// The Help window's contents, read from the first page (docs/README.md): each "##" heading is a part, each link that
/// starts an item of the list under it a page. One list for GitHub's readers and the app's.
/// </summary>
public static class HelpContents
{
    /// <summary>The contents of the documentation carried (the first page's).</summary>
    public static IReadOnlyList<HelpSection> Sections { get; } = Of(HelpDocs.Read(HelpDocs.Home), HelpDocs.Home);

    /// <summary>The contents a first page lists: its parts and, in each, the pages its list items link to first.</summary>
    /// <param name="doc">The first page, read.</param>
    /// <param name="page">Its path under docs/ (where its links start from).</param>
    public static IReadOnlyList<HelpSection> Of(MarkdownDocument doc, string page)
    {
        var sections = new List<HelpSection>();
        string? title = null;
        var entries = new List<HelpEntry>();
        void Close()
        {
            if (title != null && entries.Count > 0)
            {
                sections.Add(new HelpSection(title, entries));
            }
            entries = [];
        }
        foreach (var block in doc)
        {
            if (block is HeadingBlock { Level: 2 } h)
            {
                Close();
                title = HelpText.Of(h.Inline);
            }
            else if (block is ListBlock list && title != null)
            {
                entries.AddRange(list.OfType<ListItemBlock>().Select(item => Entry(item, page)).OfType<HelpEntry>());
            }
        }
        Close();
        return sections;
    }

    /// <summary>The page a list item names: the link it starts with, when that leads to a page carried; null otherwise.</summary>
    private static HelpEntry? Entry(ListItemBlock item, string page) =>
        item.FirstOrDefault() is ParagraphBlock { Inline: { } inline }
        && inline.FirstChild is LinkInline { IsImage: false, Url: { } url } link
        && HelpDocs.Resolve(page, url)?.Target is { } target
            ? new HelpEntry(HelpText.Of(link), target)
            : null;

    /// <summary>The contents' entry for a page (the first one when it is listed twice); null when it is not listed.</summary>
    /// <param name="page">The page, under docs/.</param>
    public static HelpEntry? Find(string page) => Sections.SelectMany(s => s.Entries).FirstOrDefault(e => e.Target.Page == page);
}
