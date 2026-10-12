using TerrainEditor.Editing;
using TerrainEditor.Save;

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
}
