using System.IO.Compression;
using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What a save changes: every other object stays byte for byte, and the zone list survives a round trip.
/// </summary>
public class WorldSaveIntegritySaveTests
{
    [Fact]
    public void ASaveLeavesEveryOtherObjectByteForByte()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var t = world.TerrainZones[0];
        var (id, prefab, position, rotation, scale) = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
        var edit = new EditStore(world).Get(t.ZoneX, t.ZoneZ)!.Clone();
        edit.Modified[500] = true;
        edit.Level[500] = 1.25f;
        var planted = new NewObject(-1, Fixtures.Hash("Beech1"), position + new Vector3(3, 0, 1), Vector3.Zero, 1f);
        // What must stay: every object but the deleted tree and the edited zone's terrain object.
        int terrainId = world.ObjectRefs.FindIndex(o => o.IsTerrain && o.Zone == (t.ZoneX, t.ZoneZ));
        var kept = Enumerable.Range(0, world.ObjectRefs.Count).Where(i => i != id && i != terrainId).Select(world.ObjectBytes).ToList();
        var r = WorldWriter.Save(world, [edit], [id], [planted]);
        Assert.True(r.Saved, r.Message);
        var again = WorldSave.Load(w.Dir);
        var after = Enumerable.Range(0, again.ObjectRefs.Count).Select(again.ObjectBytes).Select(Convert.ToBase64String).GroupBy(b => b).ToDictionary(g => g.Key, g => g.Count());
        foreach (string? bytes in kept.Select(Convert.ToBase64String))
        {
            Assert.True(after.TryGetValue(bytes, out int n) && n > 0, "an untouched object changed or went missing");
            after[bytes] = n - 1;
        }
        // What is left over: the new tree and the new terrain object.
        Assert.Equal(2, after.Values.Sum());
    }

    /// <summary>
    /// A .db2 file's parts: version and time, the zone list package (unpacked) and what follows it.
    /// </summary>
    private static (int Version, double NetTime, byte[] Package, byte[] Tail) DbParts(string path)
    {
        using var r = new BinaryReader(File.OpenRead(path));
        int version = r.ReadInt32();
        double netTime = r.ReadDouble();
        byte[] packed = r.ReadBytes(r.ReadInt32());
        byte[] tail = r.ReadBytes((int)(r.BaseStream.Length - r.BaseStream.Position));
        using var gz = new GZipStream(new MemoryStream(packed), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        gz.CopyTo(raw);
        return (version, netTime, raw.ToArray(), tail);
    }

    [Fact]
    public void TheZoneListSurvivesARoundTrip()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        string db2 = Path.Combine(w.Dir, $"_main.{world.SaveNumber}.db2");
        var db = ZoneDb.Load(db2);
        Assert.NotEmpty(db.Generated);
        string copy = Path.Combine(w.Dir, "copy.db2");
        db.Save(copy);
        // The same content: header, the zone list itself (compared unpacked: gzip's bytes depend on the
        // zlib the runtime has, and CI's packs the same data differently) and the rest of the file.
        var (a, b) = (DbParts(db2), DbParts(copy));
        Assert.Equal((a.Version, a.NetTime), (b.Version, b.NetTime));
        Assert.True(a.Package.AsSpan().SequenceEqual(b.Package), "the zone list changes when written back unchanged");
        Assert.True(a.Tail.AsSpan().SequenceEqual(b.Tail), "the rest of the file changes when written back");
        // A reset zone is no longer generated, and only that one.
        var zone = db.Generated.First();
        int count = db.Generated.Count;
        db.ResetZone(zone.X, zone.Z);
        Assert.DoesNotContain(zone, db.Generated);
        Assert.Equal(count - 1, db.Generated.Count);
    }
}
