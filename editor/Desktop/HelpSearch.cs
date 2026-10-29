using Markdig.Syntax;

namespace TerrainEditor.Desktop;

/// <summary>A part of a page found by a search: where it is, what it is called and the words around the first match.</summary>
/// <param name="Target">The page and the heading the part starts at.</param>
/// <param name="Page">The page's title.</param>
/// <param name="Heading">The part's heading (the page's title for the part before its first subheading).</param>
/// <param name="Snippet">A few words of the part around the first word searched for.</param>
public sealed record HelpHit(HelpTarget Target, string Page, string Heading, string Snippet);

/// <summary>
/// The Help window's search: every page cut into parts at its headings; a part matches when it holds every word typed
/// (the start of a word: "pain" finds "paint"), and the parts whose heading or page title hold them come first.
/// </summary>
public sealed partial class HelpSearch
{
    /// <summary>A part of a page: where it starts, its page's and its own heading, and its text, with their words (Words).</summary>
    private sealed record Part(HelpTarget Target, string Page, string Heading, string Text)
    {
        /// <summary>The words of the heading, of the page's title and of the text.</summary>
        public string[] HeadingWords { get; } = Words(Heading);

        /// <inheritdoc cref="HeadingWords"/>
        public string[] PageWords { get; } = Words(Page);

        /// <inheritdoc cref="HeadingWords"/>
        public string[] TextWords { get; } = WordsKept(Text);
    }

    /// <summary>The most parts a search lists.</summary>
    public const int MaxHits = 40;

    /// <summary>How many characters of a part a hit shows around the first match.</summary>
    private const int SnippetLength = 140;

    /// <summary>Every part of every page searched.</summary>
    private readonly List<Part> _parts = [];

    /// <summary>The search through the documentation carried, made the first time it is needed.</summary>
    public static HelpSearch Carried => CarriedSearch.Value;

    /// <summary>The documentation carried, cut into parts once.</summary>
    private static readonly Lazy<HelpSearch> CarriedSearch = new(() => new HelpSearch(HelpDocs.Pages.Select(p => (p, HelpDocs.Read(p)))));

    /// <summary>Cuts pages into parts at their headings.</summary>
    /// <param name="pages">Each page's path under docs/ and the page, read.</param>
    public HelpSearch(IEnumerable<(string Page, MarkdownDocument Doc)> pages)
    {
        foreach (var (page, doc) in pages)
        {
            Add(page, doc);
        }
    }

    /// <summary>Adds a page's parts: the text before its first subheading, then each heading's.</summary>
    private void Add(string page, MarkdownDocument doc)
    {
        string title = HelpDocs.Title(doc, page);
        var target = new HelpTarget(page);
        string heading = title;
        var text = new System.Text.StringBuilder();
        foreach (var block in doc)
        {
            if (block is HeadingBlock h && h.Level > 1)
            {
                _parts.Add(new Part(target, title, heading, text.ToString()));
                text.Clear();
                heading = HelpText.Of(h.Inline);
                target = new HelpTarget(page, HelpText.Id(h));
            }
            else if (block is not HeadingBlock)
            {
                text.Append(HelpText.Of(block)).Append(' ');
            }
        }
        _parts.Add(new Part(target, title, heading, text.ToString()));
    }

    /// <summary>The parts that hold every word of a query, best first (at most MaxHits); none for a query without words.</summary>
    /// <param name="query">What was typed.</param>
    public IReadOnlyList<HelpHit> Find(string query)
    {
        string[] words = Words(query);
        if (words.Length == 0)
        {
            return [];
        }
        return [.. _parts
            .Select(p => (Part: p, Score: Score(p, words)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(MaxHits)
            .Select(x => new HelpHit(x.Part.Target, x.Part.Page, x.Part.Heading, Snippet(x.Part.Text, words[0])))];
    }

    /// <summary>A query's words, in lower case (letters and digits; "±8 m" is "8" and "m").</summary>
    public static string[] Words(string text) => [.. WordsKept(text).Distinct()];

    /// <summary>A text's words in lower case, each time it says them (Words, repeats kept).</summary>
    private static string[] WordsKept(string text) => [.. NotWord().Split(text.ToLowerInvariant()).Where(w => w.Length > 0)];

    /// <summary>What lies between words: anything but letters and digits.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial System.Text.RegularExpressions.Regex NotWord();

    /// <summary>
    /// How well a part matches: 0 when one word is missing from it; otherwise each word counts 10 in its heading, 4 in its
    /// page's title, and 1 for each time in its text (up to 5).
    /// </summary>
    private static int Score(Part part, string[] words)
    {
        int score = 0;
        foreach (string w in words)
        {
            int inText = Math.Min(5, part.TextWords.Count(t => t.StartsWith(w, StringComparison.Ordinal)));
            int inHeading = part.HeadingWords.Any(t => t.StartsWith(w, StringComparison.Ordinal)) ? 10 : 0;
            int inPage = part.PageWords.Any(t => t.StartsWith(w, StringComparison.Ordinal)) ? 4 : 0;
            if (inText + inHeading + inPage == 0)
            {
                return 0;
            }
            score += inText + inHeading + inPage;
        }
        return score;
    }

    /// <summary>A part's text around the first place a word starts with <paramref name="word"/>, cut at spaces, "…" where cut.</summary>
    public static string Snippet(string text, string word)
    {
        text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        int at = FindWordStart(text, word);
        int start = Math.Max(0, at - (SnippetLength / 3));
        if (start > 0 && text.LastIndexOf(' ', start) is var space && space > 0)
        {
            start = space + 1;
        }
        int end = Math.Min(text.Length, start + SnippetLength);
        if (end < text.Length && text.LastIndexOf(' ', end) is var cut && cut > start)
        {
            end = cut;
        }
        return (start > 0 ? "…" : "") + text[start..end] + (end < text.Length ? "…" : "");
    }

    /// <summary>Where a word starting with <paramref name="word"/> is in a text (ignoring case); 0 when nowhere.</summary>
    private static int FindWordStart(string text, string word)
    {
        for (int i = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); i >= 0; i = text.IndexOf(word, i + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (i == 0 || !char.IsLetterOrDigit(text[i - 1]))
            {
                return i;
            }
        }
        return 0;
    }
}
