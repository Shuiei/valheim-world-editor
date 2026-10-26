using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The message's account of what a save did, in English (what Claude reads back).
/// </summary>
public class WorldWriterSummaryTests
{
    [Fact]
    public void ASaveThatDidNothingHasNoAccount()
    {
        var plan = new WorldWriter.Planned([], [], []);

        Assert.Equal("", WorldWriter.Summary(plan, []).English);
    }

    [Fact]
    public void DeletionsAreCountedAsDeleted()
    {
        var plan = new WorldWriter.Planned([], [], []) { DeletedCount = 1, Removed = 1 };

        Assert.Contains("1 deleted object(s)", WorldWriter.Summary(plan, []).English);
    }

    [Fact]
    public void AResetCountsOnlyWhatItClearedBeyondTheDeletions()
    {
        var plan = new WorldWriter.Planned([], [], []) { DeletedCount = 2, Removed = 5 };

        Assert.Contains("(3 object(s) cleared)", WorldWriter.Summary(plan, [new ZoneReset(0, 0, false, false)]).English);
    }
}
