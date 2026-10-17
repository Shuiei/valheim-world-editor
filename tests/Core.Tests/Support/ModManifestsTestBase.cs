using TerrainEditor.App;

namespace WorldEditor.Tests;

/// <summary>
/// The set-up the ModManifests tests share (ModManifests*Tests): a BepInEx folder of its own, the default places' search
/// switched off for the test, the manifests forgotten after it, and a manifest written in a mod's folder.
/// </summary>
public abstract class ModManifestsTestBase : IDisposable
{
    /// <summary>
    /// Whether the default places were searched before the test, put back after it.
    /// </summary>
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;

    /// <summary>
    /// This test's own folder, deleted after it.
    /// </summary>
    protected string Dir { get; } = Path.Combine(Path.GetTempPath(), "vwe-manifests-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// Only the folders a test gives are searched.
    /// </summary>
    protected ModManifestsTestBase()
    {
        LocalGame.SearchDefaultPlaces = false;
    }

    /// <summary>
    /// Forgets the manifests, puts the default places' search back, and deletes the folder.
    /// </summary>
    public void Dispose()
    {
        ModManifests.Forget();
        LocalGame.SearchDefaultPlaces = _defaults;
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, true);
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>Settings naming the test's BepInEx folder.</summary>
    protected AppSettings Settings => new() { BepInExFolders = [Path.Combine(Dir, "BepInEx")] };

    /// <summary>Writes a manifest in a mod's folder under plugins (made); returns its path.</summary>
    protected string Manifest(string mod, string json)
    {
        string folder = Path.Combine(Dir, "BepInEx", "plugins", mod);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, ModManifests.FileName);
        File.WriteAllText(path, json);
        return path;
    }
}
