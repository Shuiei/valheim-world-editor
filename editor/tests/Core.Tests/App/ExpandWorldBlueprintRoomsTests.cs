using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static WorldEditor.Tests.ExpandWorldEntries;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// Expand World Data's blueprint rooms (a room of the room file whose name is no game room's): the blueprint's snap
/// points, over-written by the room's connections (a place, or a piece then not made), the blueprint's size, and the room
/// the Dungeon tool builds of it, its openings at the snap points and its objects the blueprint's parts.
/// </summary>
[Collection("DataDir")]
public class ExpandWorldBlueprintRoomsTests : AppSettingsTestBase
{
    /// <summary>A blueprint "Hall": two snap points of its own, a door marker, a bush and a stone (no centre piece).</summary>
    private string Hall()
    {
        string folder = Path.Combine(Data, "PlanBuild");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Hall.blueprint"),
            "#Name:Hall\n#SnapPoints\n0;0;-4\n0;0;4\n#Pieces\nBush01;Misc;2;0;2;0;0;0;1\nwood_door;Misc;4;0;0;0;0.70710677;0;0.70710677\nPickable_Stone;Misc;-4;1;-2;0;0;0;1\n");
        return folder;
    }

    [Fact]
    public void TheRoomsConnectionsOverWriteTheBlueprintsSnapPoints()
    {
        List<string> why = [], notes = [];
        var bp = ExpandWorld.RoomBlueprintOf(Hall(), "Hall", ["1, 2, 3", "wood_door", "no_such_piece"], new ExpandWorld.DataTable([]), why, notes)!;
        // The first by its place (x, z, y), unturned; the second at the door, which is then not made; the third
        // keeps nothing but past the file's own it would be added: there is none of that name, so none.
        Assert.Equal(2, bp.SnapPoints.Count);
        Assert.Equal((new Vector3(1, 3, 2), Quaternion.Identity), bp.SnapPoints[0]);
        Assert.Equal(new Vector3(4, 0, 0), bp.SnapPoints[1].Position);
        Assert.Equal(0f, bp.Blueprints["Hall"].Single(p => p.Prefab == "wood_door").Chance);
        // Its size: the pieces' bounds with the origin among them.
        Assert.Equal(new Vector3(8, 1, 4), bp.Size);
        Assert.Empty(notes);
    }

    [Fact]
    public void ABlueprintRoomIsReadAndBuiltOfItsBlueprint()
    {
        string folder = Hall();
        var data = ExpandWorld.DataFiles.None with
        {
            Rooms =
            [
                E(("name", "Hall"), ("theme", "Cave"), ("connections", new List<object?> { Map(("position", ""), ("type", "")), Map(("position", ""), ("type", "")) })),
                E(("name", "NoSuchRoom")),
            ],
        };
        var rules = ExpandWorld.DungeonsOf(data, folder)!;
        var entry = Assert.Single(rules.Rooms!);
        Assert.NotNull(entry.Blueprint);
        Assert.Contains(data.Notes, n => n.Contains("NoSuchRoom is no game room and no blueprint", StringComparison.Ordinal));
        using var _ = Dungeons.Scope(rules);
        var room = Dungeons.RoomOf("Hall")!;
        // Saved under its own name (no game room); its size the blueprint's rounded up; its openings at the snap points.
        Assert.Equal(("Hall", StableHash.Of("Hall")), (room.Game, room.Saved));
        Assert.Equal(new Vector3(8, 1, 4), room.Size);
        Assert.Equal([new Vector3(0, 0, -4), new Vector3(0, 0, 4)], room.Openings.Select(o => o.Position));
        // Its objects: the blueprint's parts at the room's place, turned with it.
        var placed = new Dungeons.Placed(room.Hash, new Vector3(100, 5000, 40), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));
        var made = Dungeons.Contents(placed, Dungeons.KindOf(StableHash.Of("DG_Cave"))!, placed.Position, 1);
        Assert.Equal(["Bush01", "wood_door", "Pickable_Stone"], made.Select(m => m.Prefab));
        var bush = ExpandWorld.RoomBlueprintOf(folder, "Hall", [], new ExpandWorld.DataTable([]), [], [])!.Blueprints["Hall"][0];
        Assert.True(Vector3.Distance(placed.ToWorld(bush.Position), made[0].Position) < 1e-4f);
    }

    private static Dictionary<string, object?> Map(params (string Key, object? Value)[] fields) => fields.ToDictionary(f => f.Key, f => f.Value);
}
