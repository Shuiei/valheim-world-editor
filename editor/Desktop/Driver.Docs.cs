using System.Text.Json;

namespace TerrainEditor.Desktop;

/// <summary>The command of the Help window (the documentation in the app).</summary>
public static partial class Driver
{
    /// <summary>The 'docs' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdDocs(Call call)
    {
        string[] args = call.Args;
        switch (args)
        {
            case ["close"]:
                HelpWindow.Current?.Close();
                return "{}";
            case ["shot", _, ..]:
                // The rest of the line: a file name may hold spaces.
                var shown = HelpWindow.Current ?? throw new InvalidOperationException("The Help window is not open");
                await WindowShot.Save(shown, call.A[1]["shot ".Length..]);
                return DocsState(shown);
            case ["search", .. var words]:
                var w = HelpWindow.Open(HelpWindow.Current?.Shown);
                w.SearchBox.Text = string.Join(' ', words);
                return DocsState(w);
            case [string target]:
                int hash = target.IndexOf('#', StringComparison.Ordinal);
                return DocsState(HelpWindow.Open(hash < 0 ? new HelpTarget(target) : new HelpTarget(target[..hash], target[(hash + 1)..])));
            default:
                throw new ArgumentException("docs <page[#anchor]> | docs search <words> | docs shot <file.png> | docs close");
        }
    }

    /// <summary>The Help window's state: the place shown, the page's title, how many parts the search found.</summary>
    private static string DocsState(HelpWindow w) =>
        JsonSerializer.Serialize(new { page = w.Shown.Page, anchor = w.Shown.Anchor, title = w.Title, hits = w.Hits.Count });
}
