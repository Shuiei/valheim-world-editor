using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What a zone's edit keeps of what the browser sent: the game's limits, no height where none is modified, finite values
/// only, and the grid's size checked.
/// </summary>
public class ZoneEditSanitizeTests
{
    [Fact]
    public void ALevelBeyondTheGamesLimitIsHeldToIt()
    {
        var zone = new ZoneEdit(0, 0);
        zone.Modified[0] = true;
        zone.Level[0] = 20f;

        zone.Sanitize();

        Assert.Equal(EditStore.MaxLevel, zone.Level[0]);
    }

    [Fact]
    public void AHeightOfAVertexNotModifiedIsDropped()
    {
        var zone = new ZoneEdit(0, 0);
        zone.Level[1] = 3f;
        zone.Smooth[1] = 0.5f;

        zone.Sanitize();

        Assert.Equal((0f, 0f), (zone.Level[1], zone.Smooth[1]));
    }

    [Fact]
    public void ANonFinitePaintBecomesNone()
    {
        var zone = new ZoneEdit(0, 0);
        zone.Paint[0] = 2f;
        zone.Paint[1] = float.NaN;

        zone.Sanitize();

        Assert.Equal((1f, 0f), (zone.Paint[0], zone.Paint[1]));
    }

    [Fact]
    public void AGridOfTheWrongSizeIsRefused()
    {
        var zone = new ZoneEdit(0, 0) { Modified = new bool[3] };

        Assert.Throws<ArgumentException>(zone.Sanitize);
    }
}
