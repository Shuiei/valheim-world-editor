using System.Text;
using Markdig.Syntax;
using TerrainEditor.App;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A made-up translation of the documentation for the tests: its files laid over the ones carried (HelpDocs.Overlay),
/// in the language folder <see cref="Code"/>, and, when asked, that language taken (an empty catalogue of its own: the
/// app's texts stay English). Only the files carried, and English again, once disposed. It changes the language in use,
/// so a test using it runs in the DataDir collection.
/// </summary>
internal sealed class FakeDocsLanguage : IDisposable
{
    /// <summary>The language's code, and its folder's name: one no real translation has.</summary>
    public const string Code = "zz";

    /// <summary>The folder its catalogue is written to, deleted when disposed.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vwe-docs-lang-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The catalogues' folder before (the tests' or none).</summary>
    private readonly string? _folderBefore = Lang.FolderOverride;

    /// <summary>The files laid over the ones carried, by their path under docs/.</summary>
    private readonly Dictionary<string, byte[]> _files = [];

    /// <summary>Lays the pages over the ones carried and, when <paramref name="take"/>, takes the language.</summary>
    /// <param name="pages">Each page's text by its path under docs/ ("zz/map.md").</param>
    /// <param name="take">Whether to take the language (the documentation is then shown in it).</param>
    /// <exception cref="InvalidOperationException">Lang did not take it (the catalogue was refused).</exception>
    public FakeDocsLanguage(IReadOnlyDictionary<string, string> pages, bool take = true)
    {
        foreach (var (path, text) in pages)
        {
            _files[path] = Encoding.UTF8.GetBytes(text);
        }
        HelpDocs.Overlay = new Dictionary<string, byte[]>(_files);
        if (!take)
        {
            return;
        }
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, Code + ".json"), "{}");
        Lang.FolderOverride = _folder;
        if (Lang.Use(Code) != Code)
        {
            throw new InvalidOperationException("The made-up language was not taken.");
        }
    }

    /// <summary>Lays one more file over the ones carried (a picture).</summary>
    /// <param name="path">Its path under docs/.</param>
    /// <param name="bytes">Its bytes.</param>
    public void Add(string path, byte[] bytes)
    {
        _files[path] = bytes;
        // A new set of files: what was made from the ones before (the contents, the search) is made again.
        HelpDocs.Overlay = new Dictionary<string, byte[]>(_files);
    }

    /// <summary>
    /// A translation of an English page carried: its headings, at the same levels and in the same order, named "Titre 1",
    /// "Titre 2"… (ids "titre-1"…), <paramref name="body"/> under the first, and the source mark given first.
    /// </summary>
    /// <param name="page">The English page, under docs/.</param>
    /// <param name="body">The Markdown under the first heading.</param>
    /// <param name="mark">The source mark written (HelpDocs.SourceMark); none when null.</param>
    public static string Translation(string page, string body, string? mark)
    {
        var text = new StringBuilder();
        if (mark != null)
        {
            text.Append("<!-- source: ").Append(mark).Append(" -->\n\n");
        }
        int n = 0;
        foreach (var h in HelpDocs.Read(page).Descendants<HeadingBlock>())
        {
            text.Append('#', h.Level).Append(" Titre ").Append(++n).Append("\n\n");
            if (n == 1)
            {
                text.Append(body).Append("\n\n");
            }
        }
        return text.ToString();
    }

    /// <summary>The source mark of an English page carried, as its up-to-date translation carries it.</summary>
    /// <param name="page">The English page, under docs/.</param>
    public static string Current(string page) => HelpDocs.SourceMark(HelpDocs.Text(page));

    /// <summary>Only the files carried, English again, the catalogue deleted.</summary>
    public void Dispose()
    {
        HelpDocs.Overlay = null;
        if (Directory.Exists(_folder))
        {
            Lang.FolderOverride = _folderBefore;
            Lang.Use(Lang.Source);
            Directory.Delete(_folder, true);
        }
    }
}
