using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading the uncompressed zone package the live bridge sends: the generated zones, the location version, the global
/// keys, whether the locations are laid out, then each location with its hash, position and placed flag.
/// </summary>
public class ZoneDbPackageReadTests
{
    /// <summary>A zone package written in the layout the game sends.</summary>
    private static byte[] Package()
    {
        using MemoryStream ms = new();
        using (BinaryWriter w = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(2);
            w.Write((short)1);
            w.Write((short)-2);
            w.Write((short)3);
            w.Write((short)4);
            w.Write(7);
            w.Write(1);
            w.Write("a key");
            w.Write(true);
            w.Write(1);
            w.Write(42);
            w.Write(1.5f);
            w.Write(2.5f);
            w.Write(3.5f);
            w.Write(false);
        }
        return ms.ToArray();
    }

    [Fact]
    public void TheGeneratedZonesAreReadAsTheirCoordinates()
    {
        var db = ZoneDb.FromPackage(Package(), 12.5);
        Assert.Equal(2, db.Generated.Count);
        Assert.Contains(((short)1, (short)-2), db.Generated);
        Assert.Contains(((short)3, (short)4), db.Generated);
    }

    [Fact]
    public void TheVersionKeysAndLayoutFlagAreRead()
    {
        var db = ZoneDb.FromPackage(Package(), 12.5);
        Assert.Equal(7, db.LocationVersion);
        Assert.Equal(new[] { "a key" }, db.GlobalKeys);
        Assert.True(db.LocationsGenerated);
        Assert.Equal(12.5, db.NetTime);
    }

    [Fact]
    public void EachLocationIsReadWithItsHashPositionAndPlacedFlag()
    {
        var db = ZoneDb.FromPackage(Package(), 0);
        var (hash, position, placed) = Assert.Single(db.Locations);
        Assert.Equal(42, hash);
        Assert.Equal(new Vector3(1.5f, 2.5f, 3.5f), position);
        Assert.False(placed);
    }
}
