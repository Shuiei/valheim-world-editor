using System.Numerics;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// The flat 30 m zone the terrain modifier tests work on (65 by 65 vertices), the placed objects they make a world of, and
/// the heights and paint read at a vertex of zone 0, 0.
/// </summary>
internal static class TerrainModifierTestWorld
{
    /// <summary>
    /// The vertices of a zone along one side.
    /// </summary>
    internal const int N = 65;

    /// <summary>
    /// A world holding only these placed objects.
    /// </summary>
    internal static WorldSave World(params PlacedObject[] placed)
    {
        var w = new WorldSave { Directory = "none", SaveNumber = 1 };
        w.Placed.AddRange(placed);
        return w;
    }

    /// <summary>
    /// A network object of this prefab at x, y, z, made at this time.
    /// </summary>
    internal static PlacedObject Net(string prefab, float x, float y, float z, long time = 0) => new(StableHash.Of(prefab), 0, new Vector3(x, y, z), Vector3.Zero, time);

    /// <summary>
    /// A location at x, y, z, turned by this angle.
    /// </summary>
    internal static PlacedObject Loc(string location, float x, float y, float z, float turn = 0) => new(0, StableHash.Of(location), new Vector3(x, y, z), new Vector3(0, turn, 0), 0);

    /// <summary>
    /// A zone's 65 × 65 heights, all 30 m.
    /// </summary>
    internal static float[] Flat() => [.. Enumerable.Repeat(30f, N * N)];

    /// <summary>
    /// The vertex of zone 0, 0 at world x, z (the zone spans -32..32).
    /// </summary>
    internal static float At(float[] h, int x, int z) => h[(z + 32) * N + x + 32];

    /// <summary>
    /// A zone's 65 × 65 paint mask, every vertex this RGBA.
    /// </summary>
    internal static float[] Mask(float r, float g, float b, float a) => [.. Enumerable.Range(0, N * N).SelectMany(_ => new[] { r, g, b, a })];

    /// <summary>
    /// The paint of the vertex of zone 0, 0 at world x, z.
    /// </summary>
    internal static float[] PaintAt(float[] mask, int x, int z) => mask[(((z + 32) * N + x + 32) * 4)..(((z + 32) * N + x + 32) * 4 + 4)];
}
