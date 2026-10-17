using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Where a crn header's palettes and levels sit (CrunchHeader's checks of them): a palette a format needs must have entries,
/// and each level must start inside the data, after the one before it.
/// </summary>
public class CrunchHeaderLayoutTests
{
    [Fact]
    public void TheLevelsOfTheFixtureAreInOrder()
    {
        var header = new CrunchHeader(CrnFiles.Fixture("crunch-dxt5.crn"));
        Assert.Equal(header.Levels, header.LevelOfs.Length);
        Assert.All(header.LevelOfs.Zip(header.LevelOfs.Skip(1)), pair => Assert.True(pair.First < pair.Second));
    }

    [Fact]
    public void APaletteWithNoEntriesIsMissing()
    {
        // The colour endpoints' entry count (at 33 + 6, two bytes) is zero.
        byte[] crn = CrnFiles.Recheck(CrnFiles.Copy(CrnFiles.Fixture("crunch-dxt5.crn"), c => { c[39] = 0; c[40] = 0; }));
        var e = Assert.Throws<InvalidDataException>(() => new CrunchHeader(crn));
        Assert.Equal("crn palette missing", e.Message);
    }

    [Fact]
    public void ALevelStartingPastTheDataIsRefused()
    {
        // Level 0's offset (at 70) is set to the largest number of four bytes.
        byte[] crn = CrnFiles.Recheck(CrnFiles.Copy(CrnFiles.Fixture("crunch-dxt5.crn"), c => { c[70] = c[71] = c[72] = c[73] = 0xFF; }));
        var e = Assert.Throws<InvalidDataException>(() => new CrunchHeader(crn));
        Assert.Equal("crn level out of the data", e.Message);
    }

    [Fact]
    public void ALevelNotAfterTheOneBeforeItIsRefused()
    {
        // Level 1's offset (at 74) is set to level 0's.
        byte[] crn = CrnFiles.Recheck(CrnFiles.Copy(CrnFiles.Fixture("crunch-dxt5.crn"), c => { for (int k = 0; k < 4; k++) { c[74 + k] = c[70 + k]; } }));
        var e = Assert.Throws<InvalidDataException>(() => new CrunchHeader(crn));
        Assert.Equal("crn level out of the data", e.Message);
    }
}
