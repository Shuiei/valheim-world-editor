using System.Numerics;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.SaveGzipHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// Damaged terrain objects in the test world are read as ordinary objects, and ground data claiming too many points
/// is refused.
/// </summary>
public class WorldSaveTerrainDamageTests
{
    [Fact]
    public void ADamagedTerrainObjectIsReadAsAnOrdinaryObject()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        ZdoLocation source = world.TerrainZones[0].Source!;
        string path = Path.Combine(w.Dir, source.File.FileName);
        byte[] bytes = File.ReadAllBytes(path);
        // The first byte of the gzip data, after the value's length.
        bytes[(int)source.DataStart + 4] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
        var damaged = WorldSave.Load(w.Dir);
        Assert.Empty(damaged.TerrainZones);
        Assert.Equal(world.ObjectCount, damaged.ObjectCount);
        Assert.Contains(damaged.ObjectRefs, o => o.Start == source.Start && o.File.FileName == source.File.FileName && !o.IsTerrain);
    }

    // Valid gzip whose ground data stops one byte short: reading runs past its end.
    [Fact]
    public void ATruncatedTerrainObjectIsReadAsAnOrdinaryObject()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        ZdoLocation source = world.TerrainZones[0].Source!;
        string path = Path.Combine(w.Dir, source.File.FileName);
        byte[] bytes = File.ReadAllBytes(path);
        byte[] raw = Gunzip(bytes[((int)source.DataStart + 4)..(int)source.DataEnd]);
        byte[] truncated = Gzip(raw[..^1]);
        using var replaced = new MemoryStream();
        replaced.Write(bytes, 0, (int)source.DataStart);
        replaced.Write(BitConverter.GetBytes(truncated.Length));
        replaced.Write(truncated);
        replaced.Write(bytes, (int)source.DataEnd, bytes.Length - (int)source.DataEnd);
        File.WriteAllBytes(path, replaced.ToArray());
        var damaged = WorldSave.Load(w.Dir);
        Assert.Empty(damaged.TerrainZones);
        Assert.Equal(world.ObjectCount, damaged.ObjectCount);
        Assert.Contains(damaged.ObjectRefs, o => o.Start == source.Start && o.File.FileName == source.File.FileName && !o.IsTerrain);
    }

    // Ground data claiming far more points than a terrain grid has is refused before its arrays are made.
    [Fact]
    public void TerrainDataWithTooManyPointsIsRefused()
    {
        const int Points = 257 * 257 + 1;
        using var raw = new MemoryStream();
        using (var bw = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(1);            // version
            bw.Write(0);            // operations
            bw.Write(0f);           // last operation position
            bw.Write(0f);
            bw.Write(0f);
            bw.Write(0f);           // last operation radius
            bw.Write(Points);       // height points, none modified
            bw.Write(new byte[Points]);
            bw.Write(0);            // paint points
        }
        var ex = Assert.Throws<InvalidDataException>(() => TerrainZone.Decode(Vector3.Zero, 0, Gzip(raw.ToArray())));
        Assert.Contains("height points", ex.Message);
    }
}
