using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace WorldEditor.Tests;

/// <summary>
/// A blueprint location's terrain snapshots laid on New world's ground (WorldCreator.ApplySnapshots): a height as the
/// level from the zone's ground as generated, a paint as it is, over what the zone had, in each zone they reach, and
/// none when they would reach too many zones.
/// </summary>
[Collection("World files")]
public class WorldCreatorSnapshotsTests
{
    /// <summary>One node valued <paramref name="value"/> at the blueprint's centre, nodes 1 m apart.</summary>
    private static TerrainSnapshot One(Vector4 value, Vector3 center = default) => new(center, Quaternion.Identity, 1f, [[value]]);

    private static int At(int x, int z) => z * EditStore.Grid + x;

    [Fact]
    public void TheVertexUnderANodeTakesItsHeightAboveTheSpotAndItsPaint()
    {
        using var w = new TempWorld();
        var gen = new TerrainService(w.Load(), null).Generator;
        var spot = new Vector3(10, 40, 20);
        var contents = LocationContents.None with { TerrainHeights = One(new Vector4(2, 0, 0, 0)), TerrainPaints = One(new Vector4(0, 1, 0, 0.5f)) };
        var grounds = new Dictionary<(int X, int Z), ZoneEdit>();
        WorldCreator.ApplySnapshots(grounds, contents, spot, Quaternion.Identity, gen);
        var edit = Assert.Single(grounds).Value;
        int i = At(42, 52);
        float generated = BaseTerrain.BuildZone(gen, 0, 0, null)[i];
        Assert.True(edit.Modified[i]);
        Assert.Equal(2f + 40f - generated, edit.Level[i], 4);
        Assert.Equal(new Vector4(0, 1, 0, 0.5f), new Vector4(edit.Paint[i * 4], edit.Paint[i * 4 + 1], edit.Paint[i * 4 + 2], edit.Paint[i * 4 + 3]));
        Assert.True(edit.PaintModified[i]);
        // No other vertex is near a node.
        Assert.Equal((1, 1), (edit.HeightCount, edit.PaintCount));
    }

    [Fact]
    public void TheSnapshotsAreLaidOverTheZonesGroundAndReachTheNextZone()
    {
        using var w = new TempWorld();
        var gen = new TerrainService(w.Load(), null).Generator;
        var earlier = new ZoneEdit(1, 0) { Changed = true };
        earlier.Modified[At(0, 32)] = true;
        earlier.Level[At(0, 32)] = 3f;
        var grounds = new Dictionary<(int X, int Z), ZoneEdit> { [(1, 0)] = earlier };
        // At the edge between zone 0 and zone 1: the vertex is in both zones' grids.
        var contents = LocationContents.None with { TerrainHeights = One(new Vector4(1, 0, 0, 0)) };
        WorldCreator.ApplySnapshots(grounds, contents, new Vector3(32, 30, 0), Quaternion.Identity, gen);
        Assert.Equal([(0, 0), (1, 0)], grounds.Keys.Order());
        Assert.Same(earlier, grounds[(1, 0)]);
        Assert.Equal(1f + 30f - BaseTerrain.BuildZone(gen, 1, 0, null)[At(0, 32)], earlier.Level[At(0, 32)], 4);
        Assert.True(grounds[(0, 0)].Modified[At(64, 32)]);
    }

    [Fact]
    public void SnapshotsReachingTooManyZonesChangeNothing()
    {
        using var w = new TempWorld();
        var gen = new TerrainService(w.Load(), null).Generator;
        // A node 300 m out: its reach spans more than 64 zones.
        var contents = LocationContents.None with { TerrainHeights = One(new Vector4(1, 0, 0, 0), new Vector3(300, 0, 0)) };
        var grounds = new Dictionary<(int X, int Z), ZoneEdit>();
        WorldCreator.ApplySnapshots(grounds, contents, Vector3.Zero, Quaternion.Identity, gen);
        Assert.Empty(grounds);
        WorldCreator.ApplySnapshots(grounds, LocationContents.None, Vector3.Zero, Quaternion.Identity, gen);
        Assert.Empty(grounds);
    }
}
