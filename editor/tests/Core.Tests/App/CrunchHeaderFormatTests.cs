using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The texture a crn header describes (CrunchHeader's format check): one face, DXT1 or DXT5, a size and a level count read
/// here. Each change is made with its checksums right, so only the format check can refuse it.
/// </summary>
public class CrunchHeaderFormatTests
{
    /// <summary>Refused as a texture not read here.</summary>
    private static void RefusedAsNotRead(byte[] crn)
    {
        var e = Assert.Throws<InvalidDataException>(() => new CrunchHeader(CrnFiles.Recheck(crn)));
        Assert.Equal("crn texture not read here", e.Message);
    }

    [Fact]
    public void MoreThanOneFaceIsNotRead() => RefusedAsNotRead(CrnFiles.Copy(CrnFiles.Fixture("crunch-dxt5.crn"), c => c[17] = 2));

    [Fact]
    public void AFormatOtherThanDxt1OrDxt5IsNotRead() => RefusedAsNotRead(CrnFiles.Copy(CrnFiles.Fixture("crunch-dxt5.crn"), c => c[18] = 7));

    [Fact]
    public void AZeroWidthIsNotRead() => RefusedAsNotRead(CrnFiles.Copy(CrnFiles.Fixture("crunch-dxt5.crn"), c => { c[12] = 0; c[13] = 0; }));

    [Fact]
    public void ZeroLevelsIsNotRead() => RefusedAsNotRead(CrnFiles.Copy(CrnFiles.Fixture("crunch-dxt5.crn"), c => c[16] = 0));
}
