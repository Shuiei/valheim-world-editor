using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Terrain;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place panel and the mods' manifests: a prefab a manifest hides is no longer offered nor chosen once the manifests
/// change (the list is worked out again), and is not chosen again from the panel's memory or a chip; shown again once no
/// manifest hides it. Runs alone (the manifests are shared).
/// </summary>
[Collection("DataDir")]
public sealed class PlacePanelManifestTests : IDisposable
{
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-placemanifest-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _world = Fixtures.CopyFixture();

    public PlacePanelManifestTests()
    {
        LocalGame.SearchDefaultPlaces = false;
        // The window's Place panel reads its memory from the data folder: one of the test's own.
        AppSettings.DataDirOverride = Path.Combine(_dir, "data");
    }

    /// <summary>
    /// Forgets the manifests, puts the default places' search back, and deletes the folders.
    /// </summary>
    public void Dispose()
    {
        ModManifests.Forget();
        LocalGame.SearchDefaultPlaces = _defaults;
        AppSettings.DataDirOverride = _dataDir;
        TestFolderCleanup.DeleteIfExists(_dir, Path.GetDirectoryName(_world)!);
    }

    /// <summary>Writes a manifest hiding these prefabs and reads the manifests again.</summary>
    private void Hide(params string[] prefabs)
    {
        string folder = Path.Combine(_dir, "BepInEx", "plugins", "Mod");
        Directory.CreateDirectory(folder);
        string entries = string.Join(",", prefabs.Select(p => $"\"{p}\": {{ \"hidden\": true }}"));
        File.WriteAllText(Path.Combine(folder, ModManifests.FileName), $"{{ \"version\": 1, \"prefabs\": {{ {entries} }} }}");
        ModManifests.Refresh(new AppSettings { BepInExFolders = [Path.Combine(_dir, "BepInEx")] });
    }

    [AvaloniaFact]
    public void AHiddenPrefabIsNeitherOfferedNorChosen()
    {
        var (w, scene, input, p) = PlacePanelOnWorld.Open(_world);
        string kind = p.Creatable().First().Name;
        input.Tool.Chosen.Add(kind);

        Hide(kind);

        Assert.DoesNotContain(p.Creatable(), c => c.Name == kind);
        Assert.DoesNotContain(kind, input.Tool.Chosen);
        // A chip still showing it (or a click on one) does not choose it again.
        p.ChipClicked(kind, shift: false);
        Assert.DoesNotContain(kind, input.Tool.Chosen);
        // Not chosen again from the memory either.
        var tool = new PlaceTool { Scene = () => scene };
        _ = new PlacePanel(new PlaceInput(w.View, tool), () => scene, PrefabCatalog.NameOf, new PlaceMemory { Chosen = [kind] });
        Assert.DoesNotContain(kind, tool.Chosen);

        ModManifests.Forget();

        Assert.Contains(p.Creatable(), c => c.Name == kind);
    }
}
