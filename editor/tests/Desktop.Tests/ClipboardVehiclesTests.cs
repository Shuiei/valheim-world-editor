using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Copying and pasting the example vehicle mod's vehicles (editor/tests/Support/ExampleMod): a part copied brings its whole vehicle, without the ground under it (it
/// does not stand on it); each paste is a vehicle of its own (new ids, its parts naming its root), drawn in
/// the ship's shape and stored at its root.
/// </summary>
public class ClipboardVehiclesTests
{
    // The example mod's data keys.
    private static readonly int IdKey = StableHash.Of(ExampleMod.IdKey), SwivelKey = StableHash.Of(ExampleMod.MountKey);

    /// <summary>An int value of an added thing's data (null when it has none).</summary>
    private static int? IntOf(EditSession s, int index, int key) =>
        s.Edits.FindAdded(s.Scene.Things[index].Id)!.Data?.FirstOrDefault(f => f.Key == key && f.Section == "ints")?.Value is { } v
            ? int.Parse(v, CultureInfo.InvariantCulture) : null;

    /// <summary>Pastes the clip at a grid point, as the Paste tool does: one step.</summary>
    private static List<int> PasteAt(EditSession s, PasteTool paste, Vector2 at)
    {
        var add = new List<(NewObject, bool)>();
        var ids = s.ReadLinks().Ids;
        return s.Commit("Paste", g => { var (t, r, a) = paste.Apply(g, at, null, ids); add.AddRange(a); return (t, r); }, [], add);
    }

    [Fact]
    public void APartCopiedBringsItsWholeVehicleWithoutTheGround()
    {
        var s = TestScenes.Flat(2);
        var ship = TestScenes.AddShip(s);
        var clip = CopyData.FromThings(s.Scene, [ship[1]], _ => "wood_floor", (_, _) => 30)!;
        Assert.Equal(5, clip.Objects.Count);
        // As one block, its height its own (not following the ground), and no ground.
        Assert.All(clip.Objects, o => Assert.False(o.Follow));
        Assert.All(clip.Rel, r => Assert.True(float.IsNaN(r)));
        Assert.All(clip.Objects, o => Assert.Contains(o.Data!, f => ObjectLinks.IsLinkKey(f.Key)));
    }

    [Fact]
    public void EachPasteIsAVehicleOfItsOwnInTheShipsShape()
    {
        var s = TestScenes.Flat(2);
        var ship = TestScenes.AddShip(s);
        var paste = new PasteTool { Clip = CopyData.FromThings(s.Scene, [ship[0]], _ => "wood_floor", (_, _) => 30) };
        var roots = new List<int>();
        foreach (var at in new[] { new Vector2(90, 90), new Vector2(90, 110) })
        {
            var added = PasteAt(s, paste, at);
            Assert.Equal(5, added.Count);
            int root = Assert.Single(added, i => s.Scene.Things[i].Prefab == TestScenes.ShipRoot);
            int id = IntOf(s, root, IdKey)!.Value;
            roots.Add(id);
            var parts = added.Where(i => i != root).ToList();
            Assert.All(parts, i => Assert.Equal(id, IntOf(s, i, StableHash.Of(ExampleMod.VehicleKey))));
            int swivel = Assert.Single(parts, i => IntOf(s, i, IdKey) != null);
            Assert.NotEqual(TestScenes.SwivelId, IntOf(s, swivel, IdKey));
            Assert.Equal(IntOf(s, swivel, IdKey), IntOf(s, Assert.Single(parts, i => IntOf(s, i, SwivelKey) != null), SwivelKey));
            // Drawn as the ship is, round its root; stored at it.
            var r = s.Scene.Things[root].Position;
            foreach (var d in new[] { new Vector3(0, 0, -2), new Vector3(0, 0, 2), new Vector3(4, 1, 0), new Vector3(4, 1, -1) })
            {
                Assert.Contains(parts, i => Vector3.Distance(s.Scene.Things[i].Position, r + d) < 1e-3f);
            }
            Assert.All(parts, i => Assert.Equal(r, s.Edits.FindAdded(s.Scene.Things[i].Id)!.Position));
        }
        Assert.Equal(3, roots.Append(TestScenes.ShipId).Distinct().Count());
        // The original stays as it was.
        Assert.Equal(TestScenes.ShipId, s.ReadLinks().Get(s.Scene.Things[ship[0]].Id)!.Link.Id);
    }
}
