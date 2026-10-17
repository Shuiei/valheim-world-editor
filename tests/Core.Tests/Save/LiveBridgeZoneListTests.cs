using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The zones a live call asks for, as the bridge reads them: a count as an int, then an int x and an int z for each zone.
/// </summary>
public sealed class LiveBridgeZoneListTests
{
    /// <summary>Reads back the zone list the bridge would send.</summary>
    private static BinaryReader ReadZoneList(IReadOnlyCollection<(int X, int Z)> zones)
    {
        byte[] bytes = (byte[])PrivateMember.Call(typeof(LiveBridge), "ZoneList", null, zones)!;
        return new BinaryReader(new MemoryStream(bytes));
    }

    /// <summary>Two zones are a count of two, then each zone's x and z in order.</summary>
    [Fact]
    public void TwoZonesAreWrittenAsACountThenEachXAndZ()
    {
        using var r = ReadZoneList([(1, -2), (3, 4)]);

        Assert.Equal(2, r.ReadInt32());
        Assert.Equal((1, -2), (r.ReadInt32(), r.ReadInt32()));
        Assert.Equal((3, 4), (r.ReadInt32(), r.ReadInt32()));
        Assert.Equal(r.BaseStream.Length, r.BaseStream.Position);
    }

    /// <summary>No zones are a count of zero and nothing else.</summary>
    [Fact]
    public void NoZonesAreACountOfZeroAndNothingElse()
    {
        using var r = ReadZoneList([]);

        Assert.Equal(0, r.ReadInt32());
        Assert.Equal(r.BaseStream.Length, r.BaseStream.Position);
    }
}
