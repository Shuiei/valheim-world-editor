using System.IO.Compression;
using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The writer's read-back check catching each kind of damage (a zone still marked generated after a reset, objects lost
/// or gained, a saved zone missing, another zone altered), an object in the format saved before chunk files (world
/// version 39), and the player who built the most.
/// </summary>
[Collection("World files")]
public class WorldWriterCheckTests
{
    private static readonly HashSet<(int, int)> NoReset = [];

    /// <summary>
    /// A copy of the test world, and the world read from it.
    /// </summary>
    private static (string Dir, WorldSave Before) Copy()
    {
        string dir = Fixtures.CopyFixture();
        return (dir, WorldSave.Load(dir));
    }

    /// <summary>
    /// Deletes a copy of the test world, quietly.
    /// </summary>
    private static void Delete(string dir)
    {
        try
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void TheReadBackCheckPassesAnUntouchedWorld()
    {
        var (dir, before) = Copy();
        try
        {
            Assert.Null(WorldWriter.Verify(dir, before, [], 0, 0, NoReset, []));
        }
        finally
        {
            Delete(dir);
        }
    }

    [Fact]
    public void TheReadBackCheckCatchesAZoneStillGenerated()
    {
        var (dir, before) = Copy();
        try
        {
            var (x, z) = before.Zones!.Generated.First();
            string? problem = WorldWriter.Verify(dir, before, [], 0, 0, NoReset, [new ZoneReset(x, z, false, true)]);
            Assert.Equal($"zone {x}, {z} is still marked as generated", problem);
        }
        finally
        {
            Delete(dir);
        }
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void TheReadBackCheckCatchesObjectsGainedOrLost(int created, int removed)
    {
        var (dir, before) = Copy();
        try
        {
            int expected = before.ObjectCount + created - removed;
            Assert.Equal($"{before.ObjectCount} objects, expected {expected}", WorldWriter.Verify(dir, before, [], created, removed, NoReset, []));
        }
        finally
        {
            Delete(dir);
        }
    }

    [Fact]
    public void TheReadBackCheckCatchesASavedZoneMissing()
    {
        var (dir, before) = Copy();
        try
        {
            Assert.Equal("zone 99, -99 is missing", WorldWriter.Verify(dir, before, [new ZoneEdit(99, -99)], 0, 0, NoReset, []));
        }
        finally
        {
            Delete(dir);
        }
    }

    [Fact]
    public void TheReadBackCheckCatchesAnotherZoneAltered()
    {
        var (dir, before) = Copy();
        try
        {
            // What the world held before differs from the file: as if the writer had touched that zone.
            var t = before.TerrainZones[0];
            t.LevelDelta[100] += 1;
            Assert.Equal($"unchanged zone {t.ZoneX}, {t.ZoneZ} was altered", WorldWriter.Verify(dir, before, [], 0, 0, NoReset, []));
            // A zone whose ground was reset may change.
            Assert.Null(WorldWriter.Verify(dir, before, [], 0, 0, [(t.ZoneX, t.ZoneZ)], []));
        }
        finally
        {
            Delete(dir);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheReadBackCheckCatchesAnotherZonesSmoothingOrPaintAltered(bool paint)
    {
        // Only the levels and the modified flags were compared: a write bug changing these passed, and the old save
        // was deleted.
        var (dir, before) = Copy();
        try
        {
            var t = before.TerrainZones[0];
            if (paint)
            {
                t.Paint[100] += Vector4.One;
            }
            else
            {
                t.SmoothDelta[100] += 1;
            }
            Assert.Equal($"unchanged zone {t.ZoneX}, {t.ZoneZ} was altered", WorldWriter.Verify(dir, before, [], 0, 0, NoReset, []));
        }
        finally
        {
            Delete(dir);
        }
    }

    [Fact]
    public void AResetZonesTerrainObjectsMayAllGo()
    {
        // The last count allowed one terrain object lost per reset zone, though a zone holds one or two: a reset zone
        // with two was taken for terrain lost. Two objects of a zone the file does not have, as if the reset took them.
        var (dir, before) = Copy();
        try
        {
            static TerrainZone Gone() => new()
            {
                Center = new Vector3(500 * 64, 0, 500 * 64),
                ModifiedHeight = new bool[4225],
                LevelDelta = new float[4225],
                SmoothDelta = new float[4225],
                ModifiedPaint = new bool[4225],
                Paint = new Vector4[4225],
            };
            before.TerrainZones.AddRange([Gone(), Gone()]);

            Assert.Null(WorldWriter.Verify(dir, before, [], 0, 0, [(500, 500)], []));
        }
        finally
        {
            Delete(dir);
        }
    }

    /// <summary>
    /// A live snapshot holding one tree saved in the old format: before chunk files (version 40) each object also
    /// carried its sector, and a rotation was three whole floats.
    /// </summary>
    private static byte[] OldSnapshot(int prefab, Vector3 position, Vector3 rotation, float scale)
    {
        using var raw = new MemoryStream();
        using (var w = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(0x42455756);
            w.Write(1);
            w.Write("Old");
            w.Write("seed");
            w.Write(123);
            w.Write(2);
            w.Write(BitConverter.DoubleToInt64Bits(10.0));
            byte[] zones = new byte[4 + 4 + 4 + 1 + 4];
            w.Write(zones.Length);
            w.Write(zones);
            w.Write((short)39);
            w.Write(1);
            w.Write((ushort)(0x1000 | 0x2));
            w.Write((short)3);
            w.Write((short)-2);
            w.Write(position.X); w.Write(position.Y); w.Write(position.Z);
            w.Write(prefab);
            w.Write(rotation.X); w.Write(rotation.Y); w.Write(rotation.Z);
            w.Write((byte)1);
            w.Write(StableHash.Of("scaleScalar"));
            w.Write(scale);
            w.Write(77L);
            w.Write(5u);
        }
        using var gz = new MemoryStream();
        using (var z = new GZipStream(gz, CompressionLevel.Fastest, leaveOpen: true))
        {
            z.Write(raw.ToArray());
        }
        return gz.ToArray();
    }

    [Fact]
    public void AnObjectSavedBeforeChunkFilesIsRead()
    {
        int beech = StableHash.Of("Beech1");
        var world = WorldSave.LoadLive(OldSnapshot(beech, new Vector3(10, 30, 20), new Vector3(0, 90, 0), 2f), "test");
        Assert.Equal(39, world.Chunks.Single().WorldVersion);
        Assert.Equal(1, world.ObjectCount);
        var (_, prefab, position, rotation, scale) = world.Objects.Single();
        Assert.Equal(beech, prefab);
        Assert.Equal(new Vector3(10, 30, 20), position);
        Assert.Equal(new Vector3(0, 90, 0), rotation);
        Assert.Equal(new Vector3(2), scale);
        Assert.Equal((77L, 5u), world.ObjectRefs.Single().LiveId);
    }

    [Fact]
    public void TheTopBuilderIsThePlayerWithTheMostPieces()
    {
        var world = new WorldSave { Directory = "none", SaveNumber = 1 };
        Assert.Equal(0, world.TopBuilder);
        world.Creators[11] = 3;
        world.Creators[22] = 9;
        world.Creators[33] = 1;
        Assert.Equal(22, world.TopBuilder);
    }
}
