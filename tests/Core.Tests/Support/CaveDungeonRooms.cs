using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// The two rooms the dungeon tests put in a cave dungeon: an entrance and an end cap, both 5 km above the dungeon's point
/// (the end cap 8 m along z), placed where a read-back world keeps them.
/// </summary>
public static class CaveDungeonRooms
{
    /// <summary>
    /// The entrance and the end cap of a cave dungeon at <paramref name="at"/>.
    /// </summary>
    public static List<Dungeons.Placed> At(Vector3 at) =>
    [
        new(StableHash.Of("cave_new_entrance02"), at + new Vector3(0, 5000, 0), Quaternion.Identity),
        new(StableHash.Of("cave_new_endcap02"), at + new Vector3(0, 5000, 8), Quaternion.Identity),
    ];
}
