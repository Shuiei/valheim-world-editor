using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The overlay tests' scene with a ward (a guard stone) and a workbench, 35 m up on the flat area. The overlays look
/// names up by index, so the names are given along with the things.
/// </summary>
internal static class WardAndWorkbenchScene
{
    /// <summary>
    /// The two things, the ward first, and the name of each by its index.
    /// </summary>
    /// <returns>The things, and their names (null for a thing with no name).</returns>
    internal static (WorldScene.Thing[] Things, string?[] Names) Create()
    {
        var things = new[]
        {
            new WorldScene.Thing(1, StableHash.Of("guard_stone"), new Vector3(0, 35, 0), Vector3.Zero, 0, true),
            new WorldScene.Thing(2, StableHash.Of("piece_workbench"), new Vector3(10, 35, 10), Vector3.Zero, 0, true),
        };
        string?[] names = ["guard_stone", "piece_workbench"];
        return (things, names);
    }
}
