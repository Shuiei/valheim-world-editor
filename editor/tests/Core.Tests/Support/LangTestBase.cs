using System.Text.Json;
using TerrainEditor.App;

namespace WorldEditor.Tests;

/// <summary>
/// The set-up the Lang tests share (LangTests, LangCatalogueTests): a lang folder of its own that the tests write their
/// catalogues in, and the language put back to English after.
/// </summary>
public abstract class LangTestBase : IDisposable
{
    /// <summary>The temporary lang folder the tests write their catalogues in.</summary>
    protected string Folder { get; } = Path.Combine(Path.GetTempPath(), "vwe-lang-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// Points the lang folder at this test's own folder.
    /// </summary>
    protected LangTestBase()
    {
        Directory.CreateDirectory(Folder);
        Lang.FolderOverride = Folder;
    }

    /// <summary>
    /// Stops the override, puts English back in use, and deletes the lang folder.
    /// </summary>
    public void Dispose()
    {
        Lang.FolderOverride = null;
        Lang.Use(Lang.Source);
        Directory.Delete(Folder, true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Writes a catalogue of <paramref name="code"/> in the user's lang folder.</summary>
    protected void Write(string code, Dictionary<string, string> texts) =>
        File.WriteAllText(Path.Combine(Folder, code + ".json"), JsonSerializer.Serialize(texts));
}
