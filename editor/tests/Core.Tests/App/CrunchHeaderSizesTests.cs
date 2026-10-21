using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The sizes and checksums a crn header is read with (CrunchHeader's checks of its header and data sizes, and of their
/// CRCs): a size out of the file, or a change without its checksum, is refused with its reason.
/// </summary>
public class CrunchHeaderSizesTests
{
    [Fact]
    public void TheFixtureHeaderIsRead()
    {
        var header = new CrunchHeader(CrnFiles.Fixture("crunch-dxt5.crn"));
        Assert.Equal(6, header.Levels);
    }

    [Fact]
    public void AHeaderSizeBeyondTheDataIsRefused()
    {
        byte[] crn = CrnFiles.Copy(CrnFiles.Fixture("crunch-dxt5.crn"), c => { c[2] = 0xFF; c[3] = 0xFF; });
        var e = Assert.Throws<InvalidDataException>(() => new CrunchHeader(crn));
        Assert.Equal("crn sizes out of the data", e.Message);
    }

    [Fact]
    public void ADataSizeSmallerThanTheHeaderIsRefused()
    {
        byte[] crn = CrnFiles.Copy(CrnFiles.Fixture("crunch-dxt5.crn"), c => { c[6] = 0; c[7] = 0; c[8] = 0; c[9] = 10; });
        var e = Assert.Throws<InvalidDataException>(() => new CrunchHeader(crn));
        Assert.Equal("crn sizes out of the data", e.Message);
    }

    [Fact]
    public void AChangedHeaderByteWithoutItsCrcIsRefused()
    {
        byte[] crn = CrnFiles.Copy(CrnFiles.Fixture("crunch-dxt5.crn"), c => c[13] ^= 1);
        var e = Assert.Throws<InvalidDataException>(() => new CrunchHeader(crn));
        Assert.Equal("crn CRC check failed", e.Message);
    }

    [Fact]
    public void AChangedDataByteWithoutItsCrcIsRefused()
    {
        byte[] crn = CrnFiles.Copy(CrnFiles.Fixture("crunch-dxt5.crn"), c => c[^1] ^= 1);
        var e = Assert.Throws<InvalidDataException>(() => new CrunchHeader(crn));
        Assert.Equal("crn CRC check failed", e.Message);
    }
}
