using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Areas made up for the tests, without a world: flat ground and the objects given.
/// </summary>
internal static class TestScenes
{
    /// <summary>
    /// A flat block of size × size zones at 30 m, with nothing edited, and its edit session.
    /// </summary>
    internal static EditSession Flat(int size = 2, params WorldScene.Thing[] things)
    {
        int w = size * 64 + 1;
        var ground = new Ground(w, w, 0, 0, size);
        Array.Fill(ground.Base, 30f);
        var scene = new WorldScene
        {
            World = new WorldSave { Directory = "none", SaveNumber = 1 },
            Name = "test",
            X0 = 0,
            Z0 = 0,
            ZonesX = size,
            ZonesZ = size,
            W = w,
            H = w,
            Heights = [.. Enumerable.Repeat(30f, w * w)],
            Biomes = new int[w * w],
            Things = [.. things],
            BiomeColor = new byte[w * w * 4],
            Cx = -32 + (w - 1) / 2f,
            Cz = -32 + (w - 1) / 2f,
            Mask = new byte[w * w * 4],
            OceanDepth = new float[w * w],
            Limit = new float[w * w],
        };
        var session = new EditSession(scene, ground, new EditStore(scene.World));
        scene.Session = session;
        return session;
    }

    /// <summary>
    /// The one-zone scene of the zone that holds the first object of this kind in the world at <paramref name="dir"/>,
    /// its edit session made.
    /// </summary>
    internal static WorldScene ZoneHolding(string dir, int prefab)
    {
        var at = WorldSave.Load(dir).Objects.First(o => o.Prefab == prefab).Position;
        return WorldScene.Load(dir, (int)MathF.Floor((at.X + 32) / 64), (int)MathF.Floor((at.Z + 32) / 64), 1);
    }

    /// <summary>
    /// A flat 1 × 1 zone area at 35 m, with the given objects.
    /// </summary>
    internal static WorldScene FlatScene(params WorldScene.Thing[] things) => new()
    {
        World = null!,
        Name = "test",
        X0 = 0,
        Z0 = 0,
        ZonesX = 1,
        ZonesZ = 1,
        W = 65,
        H = 65,
        Heights = [.. Enumerable.Repeat(35f, 65 * 65)],
        Biomes = new int[65 * 65],
        Things = [.. things],
        BiomeColor = new byte[65 * 65 * 4],
        Mask = new byte[65 * 65 * 4],
        OceanDepth = new float[65 * 65],
        Limit = new float[65 * 65],
    };

    /// <summary>
    /// A beech (Beech1) standing upright at full size at the point given, not yet saved in the world.
    /// </summary>
    internal static WorldScene.Thing Beech(int id, Vector3 at) => new(id, StableHash.Of("Beech1"), at, Vector3.Zero, 1, false);

    /// <summary>
    /// The game given opened live, and its area of radius 1 around zone (0, 0) with its edit session.
    /// </summary>
    internal static async Task<(WorldSession World, EditSession Area)> LiveArea(FakeGame game)
    {
        var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
        return (world, WorldScene.Load(world, 0, 0, 1).Session!);
    }

    /// <summary>
    /// The area's first saved object moved 3 m, then left out of a save the game makes meanwhile (on the world's save in
    /// <paramref name="dir"/>, the editor's base kept): the clash bringing the changes over finds.
    /// </summary>
    /// <returns>The object, as it was before the move.</returns>
    internal static WorldScene.Thing MovedThenRemovedByTheGame(EditSession s, string dir)
    {
        int index = s.Scene.Things.FindIndex(t => t.Id >= 0 && !t.Gone);
        var thing = s.Scene.Things[index];
        s.Move([(index, thing.Position + new Vector3(3, 0, 3), thing.Rotation)]);
        Assert.True(WorldWriter.Save(WorldSave.Load(dir), [], [thing.Id], options: new WorldWriter.Options(KeepBase: true)).Saved);
        return thing;
    }

    /// <summary>The ship's root's id (AddShip).</summary>
    internal const int ShipId = 500;

    /// <summary>The prefab standing in for the ship's root (AddShip).</summary>
    internal static readonly int ShipRoot = StableHash.Of("stone_wall_2x1");

    /// <summary>The ship's swivel's id (AddShip).</summary>
    internal const int SwivelId = 501;

    /// <summary>
    /// A ship (the example vehicle mod's) added to the session in one step, every part stored at its root as the game saves them: a root
    /// (a stone wall standing in for the mod's ship, which the test data does not know) at (x, 30, z) turned 90°, floors 2 m east and west of it in its frame
    /// (south and north in the world), a swivel 4 m north of it and 1 m up (east in the world) and a floor 1 m east on
    /// the swivel (south of it in the world).
    /// </summary>
    /// <returns>Their indices: the root, the two floors, the swivel, the floor on it.</returns>
    internal static List<int> AddShip(EditSession s, float x = 40, float z = 40)
    {
        var at = new Vector3(x, 30, z);
        int floor = StableHash.Of("wood_floor");
        (NewObject, bool) Part(byte[] raw) => (new NewObject(0, floor, at, Vector3.Zero, 0, null, false, raw), true);
        return s.Commit("A ship", null, [],
        [
            (new NewObject(0, ShipRoot, at, new Vector3(0, 90, 0), 0, null, false, MadeObjects.VehicleRoot("stone_wall_2x1", at, new Vector3(0, 90, 0), ShipId)), true),
            Part(MadeObjects.VehiclePart("wood_floor", at, ShipId, new Vector3(2, 0, 0), Vector3.Zero)),
            Part(MadeObjects.VehiclePart("wood_floor", at, ShipId, new Vector3(-2, 0, 0), Vector3.Zero)),
            Part(MadeObjects.VehiclePart("wood_floor", at, ShipId, new Vector3(0, 1, 4), Vector3.Zero, persistentId: SwivelId)),
            Part(MadeObjects.VehiclePart("wood_floor", at, ShipId, new Vector3(1, 0, 0), Vector3.Zero, SwivelId)),
        ]);
    }
}
