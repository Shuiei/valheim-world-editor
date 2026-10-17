using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The data of a save's zone list and terrain edits: the zone list's keys, locations and trailing bytes, the edit store with
/// zones it cannot use, and zone data of the wrong size is refused.
/// </summary>
public class ZdoDataZoneListTests
{
    /// <summary>
    /// A zone list as the live bridge sends it: two generated zones, two global keys, three locations.
    /// </summary>
    private static ZoneDb Package()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(2);
            w.Write((short)1); w.Write((short)2);
            w.Write((short)-3); w.Write((short)4);
            w.Write(7);
            w.Write(2);
            w.Write("defeated_eikthyr");
            w.Write("nomap");
            w.Write(true);
            w.Write(3);
            foreach (var (hash, x, z, placed) in new[] { (11, 64f, 128f, true), (12, 70f, 130f, false), (13, -190f, 260f, true) })
            {
                w.Write(hash);
                w.Write(x); w.Write(30f); w.Write(z);
                w.Write(placed);
            }
        }
        return ZoneDb.FromPackage(ms.ToArray(), 1234.5);
    }

    [Fact]
    public void TheZoneListKeepsItsKeysAndLocationsThroughASave()
    {
        var db = Package();
        Assert.Equal(new[] { "defeated_eikthyr", "nomap" }, db.GlobalKeys);
        Assert.True(db.LocationsGenerated);
        Assert.Equal(7, db.LocationVersion);
        string path = Path.Combine(Path.GetTempPath(), "vwe-db-" + Guid.NewGuid().ToString("N")[..8] + ".db2");
        try
        {
            db.Save(path);
            var again = ZoneDb.Load(path);
            Assert.Equal(db.Generated, again.Generated);
            Assert.Equal(db.GlobalKeys, again.GlobalKeys);
            Assert.Equal(db.Locations, again.Locations);
            Assert.Equal(1234.5, again.NetTime);
            Assert.Equal(41, again.FileVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AResetZoneUnplacesOnlyItsOwnLocations()
    {
        var db = Package();
        // Zone 1, 2 holds the first two locations (x 64-70, z 128-130); the third is elsewhere.
        db.ResetZone(1, 2);
        Assert.DoesNotContain(((short)1, (short)2), db.Generated);
        Assert.Contains(((short)-3, (short)4), db.Generated);
        Assert.False(db.Locations[0].Placed);
        Assert.False(db.Locations[1].Placed);
        Assert.True(db.Locations[2].Placed);
    }

    [Fact]
    public void TheStoreLeavesOutSavedZonesOfAnotherSize()
    {
        var world = new WorldSave { Directory = "none", SaveNumber = 1 };
        world.TerrainZones.Add(new TerrainZone
        {
            Center = new Vector3(64, 0, 0),
            ModifiedHeight = new bool[33 * 33],
            LevelDelta = new float[33 * 33],
            SmoothDelta = new float[33 * 33],
            ModifiedPaint = new bool[33 * 33],
            Paint = new Vector4[33 * 33],
        });
        var store = new EditStore(world);
        Assert.Null(store.Get(1, 0));
        Assert.Empty(store.All());
    }

    [Fact]
    public void ZonesMarkedUnchangedAreNoLongerPending()
    {
        var store = new EditStore(new WorldSave { Directory = "none", SaveNumber = 1 });
        var e = new ZoneEdit(2, 3);
        e.Modified[10] = true;
        e.Level[10] = 1;
        store.Put(e);
        Assert.Equal(1, store.ChangedZoneCount);
        int v = store.Version;
        store.MarkUnchanged([(2, 3), (9, 9)]);
        Assert.Equal(0, store.ChangedZoneCount);
        Assert.True(store.Version > v);
    }

    [Fact]
    public void PaintMakesGroundDifferent()
    {
        var a = new ZoneEdit(0, 0);
        var b = a.Clone();
        Assert.True(a.SameGround(b));
        a.PaintModified[5] = b.PaintModified[5] = true;
        a.Paint[5 * 4] = 1;
        Assert.False(a.SameGround(b));
        b.Paint[5 * 4] = 1;
        Assert.True(a.SameGround(b));
        b.PaintModified[6] = true;
        Assert.False(a.SameGround(b));
        // Height split between level and smoothing: the same ground.
        a.Modified[7] = b.Modified[7] = true;
        a.Level[7] = 2;
        b.Level[7] = 1.5f;
        b.Smooth[7] = 0.5f;
        b.PaintModified[6] = false;
        Assert.True(a.SameGround(b));
    }

    [Fact]
    public void ZoneDataOfTheWrongSizeIsRefused()
    {
        var e = new ZoneEdit(0, 0) { Level = new float[10] };
        Assert.Throws<ArgumentException>(e.Sanitize);
    }
}
