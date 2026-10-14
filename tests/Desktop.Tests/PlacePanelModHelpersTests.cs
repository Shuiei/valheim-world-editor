using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place panel and the mods' helpers: a mod's prefab the game draws nothing of (its reported model has no parts:
/// a sound, an effect) is neither offered nor chosen once the game reports it, while one with a part and a pickable
/// without one still are; offered again once the catalogue forgets it. Runs alone (the mods' catalogue is shared), in a
/// data folder of its own.
/// </summary>
[Collection("DataDir")]
public sealed class PlacePanelModHelpersTests : IDisposable
{
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-placehelpers-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _world = Fixtures.CopyFixture();

    public PlacePanelModHelpersTests()
    {
        LocalGame.SearchDefaultPlaces = false;
        // The catalogue and the Place panel's memory are kept in the data folder: one of the test's own.
        AppSettings.DataDirOverride = Path.Combine(_dir, "data");
        ModCatalogue.Forget();
    }

    /// <summary>
    /// Forgets the mods, puts the default places' search, the tests' copies and the data folder back, and deletes the
    /// folders.
    /// </summary>
    public void Dispose()
    {
        ModCatalogue.Forget();
        GameData.UseCopies(Fixtures.Root);
        LocalGame.SearchDefaultPlaces = _defaults;
        AppSettings.DataDirOverride = _dataDir;
        foreach (string dir in (string[])[_dir, Path.GetDirectoryName(_world)!])
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>A model as the game reports it (POST /models): no parts, or one.</summary>
    private static string Model(bool part) =>
        $$"""{"s":[1,1,1],"p":[{{(part ? """{"mesh":"Cube","v":24,"sub":0,"m":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}""" : "")}}]}""";

    [AvaloniaFact]
    public void AModsPrefabTheGameDrawsNothingOfIsNotOffered()
    {
        var w = new MainWindow(load: false);
        var scene = WorldScene.Load(_world, 0, 0, 1);
        var input = new PlaceInput(w.View, new PlaceTool { Scene = () => scene });
        var p = new PlacePanel(input, () => scene, PrefabCatalog.NameOf, new PlaceMemory());
        var others = p.Creatable().Where(c => c.Kind != ObjectKind.Pickables && PrefabCatalog.ItemKindOf(StableHash.Of(c.Name)) == null).Select(c => c.Name).ToList();
        string helper = others[0], drawn = others[1], pickable = p.Creatable().First(c => c.Kind == ObjectKind.Pickables).Name;
        input.Tool.Chosen.Add(helper);

        // Reported as mods' prefabs (none is the game's here), with their models.
        string[] names = [helper, drawn, pickable];
        ModCatalogue.Take("{\"prefabs\":{" + string.Join(",", names.Select(n => $"\"{n}\":{{\"p\":1,\"d\":0,\"t\":0}}")) + "}}", new HashSet<string>());
        Assert.Equal(3, ModCatalogue.TakeModels($$"""{"{{helper}}":{{Model(false)}},"{{drawn}}":{{Model(true)}},"{{pickable}}":{{Model(false)}}}"""));

        Assert.DoesNotContain(p.Creatable(), c => c.Name == helper);
        Assert.DoesNotContain(helper, input.Tool.Chosen);
        Assert.Contains(p.Creatable(), c => c.Name == drawn);
        Assert.Contains(p.Creatable(), c => c.Name == pickable);
        // Not chosen again from the memory either.
        var tool = new PlaceTool { Scene = () => scene };
        _ = new PlacePanel(new PlaceInput(w.View, tool), () => scene, PrefabCatalog.NameOf, new PlaceMemory { Chosen = [helper, drawn] });
        Assert.DoesNotContain(helper, tool.Chosen);
        Assert.Contains(drawn, tool.Chosen);

        ModCatalogue.Forget();

        Assert.Contains(p.Creatable(), c => c.Name == helper);
    }
}
