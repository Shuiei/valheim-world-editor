using System.Numerics;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// The test world's first beech tree: the tests that delete, move or check a tree of the world read it from here.
/// </summary>
public static class TestWorldTree
{
    /// <summary>
    /// The copy's save as read, and its first beech tree (an object of the save).
    /// </summary>
    public static (WorldSave World, (int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale) Tree) Open(TempWorld w)
    {
        WorldSave world = w.Load();
        return (world, world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1")));
    }
}
