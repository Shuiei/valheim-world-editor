using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Rewriting a terrain object for a saved zone: its position becomes the zone's centre (in the short format), its data
/// is replaced with the new data behind a length, and what follows the old data is kept. An object not in the short
/// position format is refused rather than written wrongly.
/// </summary>
public sealed class WorldWriterTerrainObjectTests
{
    /// <summary>A short-format object takes the zone's centre and the new data, and keeps its tail.</summary>
    [Fact]
    public void ANewTerrainObjectTakesTheZoneCentreAndTheNewData()
    {
        // Ten bytes: a 6-byte head (flags, the position shorts), 2 bytes of old data, 2 bytes of tail.
        byte[] own = [0xAA, 0xBB, 1, 2, 3, 4, 5, 6, 0xCC, 0xDD];
        var src = new ZdoLocation(new ChunkFile(), 0, 10, 6, 8, ZdoData.SmallPositionFlag);
        var edit = new ZoneEdit(1, 2);
        byte[] data = [0x11, 0x22, 0x33];

        byte[] written = (byte[])PrivateMember.Call(typeof(WorldWriter), "NewTerrainObject", null, src, own, edit, data)!;

        // Position: x = 1 * 64 = 64 and z = 2 * 64 = 128 as shorts; then the data's length as an int, the data, the tail.
        Assert.Equal(
            new byte[] { 0xAA, 0xBB, 0x40, 0x00, 0x80, 0x00, 0x03, 0x00, 0x00, 0x00, 0x11, 0x22, 0x33, 0xCC, 0xDD },
            written);
    }

    /// <summary>An object not in the short position format is refused.</summary>
    [Fact]
    public void AnObjectNotInTheShortFormatIsRefused()
    {
        byte[] own = new byte[10];
        var src = new ZdoLocation(new ChunkFile(), 0, 10, 6, 8, 0);

        Assert.Throws<InvalidOperationException>(() =>
            PrivateMember.Call(typeof(WorldWriter), "NewTerrainObject", null, src, own, new ZoneEdit(0, 0), new byte[1]));
    }
}
