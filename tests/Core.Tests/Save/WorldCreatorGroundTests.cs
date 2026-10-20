using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace WorldEditor.Tests;

/// <summary>
/// The ground Expand World Data levels and paints round a location in New world: the level towards the spot over the
/// zone's inner heights, blended over the border; the paint on the mod's 64 × 64 grid as the game widens it; the
/// ground the file's objects snap to; the zone's terrain object.
/// </summary>
[Collection("World files")]
public class WorldCreatorGroundTests
{
    /// <summary>The zone's ground as a location at <paramref name="at"/> in the middle zone changes it.</summary>
    private static ZoneEdit? Ground(TempWorld w, LocationContents contents, Vector3 at, out TerrainService terrain)
    {
        terrain = new TerrainService(w.Load(), null);
        return WorldCreator.GroundOf(contents, at, (0, 0), terrain.Generator);
    }

    /// <summary>The index of a point of the 65 × 65 grid.</summary>
    private static int At(int x, int z) => z * EditStore.Grid + x;

    [Fact]
    public void NothingLevelledOrPaintedChangesNoGround()
    {
        using var w = new TempWorld();
        Assert.Null(Ground(w, LocationContents.None, new Vector3(10, 40, 20), out _));
    }

    [Fact]
    public void TheGroundIsLevelledTowardsTheSpot()
    {
        using var w = new TempWorld();
        // The spot on the grid point (42, 52), as the mod measures it (half a metre off the vertex).
        var spot = new Vector3(10.5f, 40f, 20.5f);
        var edit = Ground(w, new LocationContents { Level = new LevelArea(5f, 5f) }, spot, out var terrain)!;
        float[] generated = terrain.BaseZone(0, 0);
        // Within the radius, fully; 7 m away (0.7 of the reach, half of it the border), 60 % of the way.
        Assert.Equal(40f - generated[At(42, 52)], edit.Level[At(42, 52)], 4);
        Assert.Equal(0.6f * (40f - generated[At(49, 52)]), edit.Level[At(49, 52)], 3);
        Assert.True(edit.Modified[At(49, 52)]);
        // Beyond the reach, nothing.
        Assert.False(edit.Modified[At(53, 52)]);
        Assert.DoesNotContain(true, edit.PaintModified);
    }

    [Fact]
    public void TheZonesEdgeIsNeverLevelled()
    {
        using var w = new TempWorld();
        var edit = Ground(w, new LocationContents { Level = new LevelArea(3f, 0f) }, new Vector3(-31.5f, 40f, 0.5f), out _)!;
        Assert.False(edit.Modified[At(0, 32)]);
        Assert.True(edit.Modified[At(1, 32)]);
    }

    [Fact]
    public void PaintIsBlendedOnTheModsGridAndWidenedAsTheGameReadsIt()
    {
        using var w = new TempWorld();
        // At the zone's far corner: the paint cell (63, 63) is measured from its corner (32, 32).
        var edit = Ground(w, new LocationContents { Paint = new PaintArea(1f, 0f, new Vector3(1f, 0f, 0f), 0.5f) }, new Vector3(32f, 40f, 32f), out _)!;
        foreach (int i in new[] { At(63, 63), At(64, 63), At(63, 64), At(64, 64) })
        {
            Assert.True(edit.PaintModified[i]);
            Assert.Equal(new Vector4(1f, 0f, 0f, 0.5f), new Vector4(edit.Paint[i * 4], edit.Paint[i * 4 + 1], edit.Paint[i * 4 + 2], edit.Paint[i * 4 + 3]));
        }
        Assert.False(edit.PaintModified[At(62, 62)]);
        Assert.DoesNotContain(true, edit.Modified);
    }

    [Fact]
    public void TheLevelledZoneHoldsTheLevelToEightMetres()
    {
        using var w = new TempWorld();
        var terrain = new TerrainService(w.Load(), null);
        var edit = new ZoneEdit(0, 0);
        edit.Modified[At(10, 10)] = true;
        edit.Level[At(10, 10)] = 20f;
        var levelled = WorldCreator.LevelledOf(edit, terrain, 0, Vector3.Zero, 0f)!;
        Assert.Equal(terrain.BaseZone(0, 0)[At(10, 10)] + 8f, levelled.Heights[At(10, 10)], 4);
        Assert.Equal(terrain.BaseZone(0, 0)[At(11, 10)], levelled.Heights[At(11, 10)]);
        Assert.Null(WorldCreator.LevelledOf(null, terrain, 0, Vector3.Zero, 0f));
    }

    [Fact]
    public void AChangedZonesTerrainObjectHoldsItsGround()
    {
        var edit = new ZoneEdit(2, -1);
        edit.Modified[At(5, 6)] = true;
        edit.Level[At(5, 6)] = 1.5f;
        edit.PaintModified[At(7, 8)] = true;
        edit.Paint[At(7, 8) * 4 + 2] = 1f;
        var z = ZdoData.Parse(WorldCreator.TerrainObject(edit));
        Assert.Equal(new Vector3(128f, 0f, -64f), z.Position);
        var zone = TerrainZone.Decode(z.Position, StableHash.Of("_TerrainCompiler"), z.ByteList.Single(b => b.Key == StableHash.Of("TCData")).Value);
        Assert.Equal(1.5f, zone.LevelDelta[At(5, 6)]);
        Assert.Equal(new Vector4(0f, 0f, 1f, 0f), zone.Paint[At(7, 8)]);
    }
}
