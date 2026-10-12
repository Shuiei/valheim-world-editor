using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The cursor on a dungeon's rooms with models made in code: rooms without a model or whose parts give no triangle are
/// passed over, a ray beside a room's box meets nothing, the nearest triangle and the nearest room win, a wall seen
/// edge on and a floor behind the ray are not met, and a surface above the cut does not count.
/// </summary>
public class RoomSurfacesBranchesTests
{
    private static readonly Vector3 At = new(100, 5000, 50);

    /// <summary>
    /// Models by room name: an end cap with a floor (2 m below its origin, x 2 to 6, z -1 to 1), a small shelf 1 m
    /// below its origin and a wall along x = 4; crossroads with parts that give no triangle; no model for others.
    /// </summary>
    private sealed class Rooms : ModelStore.ISource
    {
        public string? ObjectName(int prefab) => null;

        public ModelStore.Model? Model(string name) => name switch
        {
            "cave_new_endcap02" => new ModelStore.Model(name, [new ModelStore.Part("floor", 0, "x", Matrix4x4.Identity)], Vector3.One),
            "cave_new_crossroads01" => new ModelStore.Model(name, [new ModelStore.Part("missing", 0, "x", Matrix4x4.Identity), new ModelStore.Part("floor", 5, "x", Matrix4x4.Identity)], Vector3.One),
            _ => null,
        };

        public ModelStore.MeshData? Mesh(string id)
        {
            if (id != "floor")
            {
                return null;
            }
            // Positions in the room's frame, stored with z mirrored as the models are.
            static float[] Vertex(float x, float y, float z) => [x, y, -z, 0, 1, 0, 0, 0];
            float[] v =
            [
                // The shelf.
                .. Vertex(2, -1, -1), .. Vertex(3, -1, -1), .. Vertex(2, -1, 0),
                // The floor.
                .. Vertex(2, -2, -1), .. Vertex(6, -2, -1), .. Vertex(6, -2, 1), .. Vertex(2, -2, 1),
                // The wall.
                .. Vertex(4, -2, -1), .. Vertex(4, 0, -1), .. Vertex(4, 0, 1),
            ];
            return new ModelStore.MeshData(v, [[0, 1, 2, 3, 4, 5, 3, 5, 6, 7, 8, 9]]);
        }

        public ModelStore.MaterialData Material(string id) => new(Vector4.One, null, 0, false, new Vector4(1, 1, 0, 0));

        public ModelStore.ImageData? Texture(string id) => null;
    }

    /// <summary>
    /// One Frost Cave of these rooms.
    /// </summary>
    private static DungeonRooms.Dungeon[] Cave(params Dungeons.Placed[] rooms) => [new DungeonRooms.Dungeon(0, default, Dungeons.KindOf(StableHash.Of("DG_Cave"))!, [.. rooms], null)];

    /// <summary>
    /// A room of this name at a place, unturned.
    /// </summary>
    private static Dungeons.Placed Room(string name, Vector3 at) => new(StableHash.Of(name), at, Quaternion.Identity);

    [Fact]
    public void TheNearestSurfaceBelowTheCutIsMet()
    {
        var models = new ModelStore(new Rooms());
        var cave = Cave(Room("cave_new_endcap02", At));
        var down = -Vector3.UnitY;
        // Over the shelf: met before the floor under it.
        Assert.Equal(11f, RoomSurfaces.Hit(cave, models, At + new Vector3(2.5f, 10, -0.9f), down)!.Value, 3);
        // The shelf above the cut: the floor.
        Assert.Equal(12f, RoomSurfaces.Hit(cave, models, At + new Vector3(2.5f, 10, -0.9f), down, maxY: At.Y - 1.5f)!.Value, 3);
        // Over each half of the floor, beside the shelf.
        Assert.Equal(12f, RoomSurfaces.Hit(cave, models, At + new Vector3(5.5f, 10, 0.9f), down)!.Value, 3);
        Assert.Equal(12f, RoomSurfaces.Hit(cave, models, At + new Vector3(2.5f, 10, 0.75f), down)!.Value, 3);
        Assert.Equal(12f, RoomSurfaces.Hit(cave, models, At + new Vector3(3.5f, 10, -0.75f), down)!.Value, 3);
        // From between the floor and the shelf, looking up: the shelf, not the floor behind.
        Assert.Equal(0.5f, RoomSurfaces.Hit(cave, models, At + new Vector3(2.5f, -1.5f, -0.9f), Vector3.UnitY)!.Value, 3);
        // Beside the room.
        Assert.Null(RoomSurfaces.Hit(cave, models, At + new Vector3(20, 10, 0), down));
    }

    [Fact]
    public void RoomsWithNoTrianglesArePassedOverAndTheNearestRoomWins()
    {
        var models = new ModelStore(new Rooms());
        var cave = Cave(
            // Not a room, a room with no model, one whose parts give no triangle.
            new Dungeons.Placed(StableHash.Of("no_such_room"), At, Quaternion.Identity),
            Room("cave_new_corridor03", At),
            Room("cave_new_crossroads01", At),
            // Two end caps, one 10 m under the other.
            Room("cave_new_endcap02", At),
            Room("cave_new_endcap02", At - new Vector3(0, 10, 0)));
        Assert.Equal(12f, RoomSurfaces.Hit(cave, models, At + new Vector3(5.5f, 10, 0.9f), -Vector3.UnitY)!.Value, 3);
        // Only rooms with nothing to meet.
        Assert.Null(RoomSurfaces.Hit(Cave(Room("cave_new_corridor03", At), Room("cave_new_crossroads01", At)), models, At + new Vector3(5.5f, 10, 0.9f), -Vector3.UnitY));
    }
}
