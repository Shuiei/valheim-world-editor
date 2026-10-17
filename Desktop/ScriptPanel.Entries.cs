using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The list: the examples, then the saved scripts, and choosing an entry.
public sealed partial class ScriptPanel
{
    /// <summary>
    /// Fills the list: the examples, then the saved scripts (newest first); selects the entry of that name if given,
    /// else none.
    /// </summary>
    public void Fill(string? select = null)
    {
        _filling = true;
        _entries = [.. ScriptExamples.All.Select(e => (e.Name, e.Code, true))];
        try
        {
            if (Directory.Exists(Folder))
            {
                foreach (var f in new DirectoryInfo(Folder).GetFiles("*.csx").OrderByDescending(f => f.LastWriteTimeUtc))
                {
                    // One that cannot be read is left out (and logged), not the rest with it.
                    try
                    {
                        _entries.Add((Path.GetFileNameWithoutExtension(f.Name), File.ReadAllText(f.FullName), false));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Log.Caught(ex, "reading a script to list it (it is left out)");
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "listing the scripts folder");
        }
        ScriptBox.ItemsSource = _entries.Select(e => e.Example ? Lang.T("script.example", e.Name) : e.Name).ToList();
        ScriptBox.SelectedIndex = select != null ? _entries.FindIndex(e => !e.Example && e.Name == select) : -1;
        _filling = false;
    }
}
