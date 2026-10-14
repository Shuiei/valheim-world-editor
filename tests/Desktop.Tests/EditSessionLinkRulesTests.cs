using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// An edit session and a mod's own link rule (its worldeditor.json): a made-up mod whose loads hang on their cart and
/// are saved where they are drawn. Moved or turned, the cart takes its load along, the load kept where it is drawn,
/// and the messages call the group as the mod does. Runs alone (the manifests are shared).
/// </summary>
[Collection("DataDir")]
public sealed class EditSessionLinkRulesTests : IDisposable
{
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-linkrules-" + Guid.NewGuid().ToString("N")[..8]);

    public EditSessionLinkRulesTests()
    {
        LocalGame.SearchDefaultPlaces = false;
        string folder = Path.Combine(_dir, "BepInEx", "plugins", "Carts");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ModManifests.FileName), """
            {
              "version": 1,
              "links": [{ "name": "cart", "id": "CartId", "parents": ["CartParent"], "offset": "CartOffset", "storedAt": "own" }]
            }
            """);
        ModManifests.Refresh(new AppSettings { BepInExFolders = [Path.Combine(_dir, "BepInEx")] });
    }

    /// <summary>
    /// Forgets the manifests, puts the default places' search back, and deletes the folder.
    /// </summary>
    public void Dispose()
    {
        ModManifests.Forget();
        LocalGame.SearchDefaultPlaces = _defaults;
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Whether two places are within a millimetre.</summary>
    private static void Near(Vector3 want, Vector3 got) => Assert.True(Vector3.Distance(want, got) < 1e-3f, $"{want} != {got}");

    [Fact]
    public void ACartTakesItsLoadAlongKeptWhereItIsDrawn()
    {
        var s = TestScenes.Flat(2);
        var at = new Vector3(40, 30, 40);
        int prefab = StableHash.Of("wood_floor");
        var made = s.Commit("A cart", null, [],
        [
            (new NewObject(0, prefab, at, Vector3.Zero, 0, null, false, MadeObjects.Zdo("wood_floor", at, z => z.IntList.Add((StableHash.Of("CartId"), 7)))), true),
            (new NewObject(0, prefab, at + new Vector3(1, 1, 0), Vector3.Zero, 0, null, false, MadeObjects.Zdo("wood_floor", at + new Vector3(1, 1, 0), z =>
            {
                z.IntList.Add((StableHash.Of("CartParent"), 7));
                z.Vec3List.Add((StableHash.Of("CartOffset"), new Vector3(1, 1, 0)));
            })), true),
        ]);
        var (_, _, groups, noun) = s.WithLinked([made[1]]);
        Assert.Equal((1, "cart"), (groups, noun));

        // The load moved 5 m east: the cart goes along, and the load is saved where it is now drawn.
        var load = s.Scene.Things[made[1]];
        var moved = s.Move([(made[1], load.Position + new Vector3(5, 0, 0), load.Rotation)]);
        Assert.Equal(2, moved.Count);
        Near(new Vector3(46, 31, 40), s.Scene.Things[moved[0]].Position);
        Near(new Vector3(45, 30, 40), s.Scene.Things[moved[1]].Position);
        Assert.Contains(s.Edits.Added, n => Vector3.Distance(n.Position, new Vector3(46, 31, 40)) < 1e-3f);

        // The cart turned a quarter: the load goes round with it (east in its frame is south), saved there.
        var turned = s.Transform([(moved[1], s.Scene.Things[moved[1]].Position, new Vector3(0, 90, 0), 0)]);
        Near(new Vector3(45, 31, 39), s.Scene.Things[turned[1]].Position);
        Assert.Equal(90, s.Scene.Things[turned[1]].Rotation.Y, 2);
        Assert.Contains(s.Edits.Added, n => Vector3.Distance(n.Position, new Vector3(45, 31, 39)) < 1e-3f);
    }
}
