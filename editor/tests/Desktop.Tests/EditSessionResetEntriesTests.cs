using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The reset entries a step puts on the history for one zone (EditSession.ResetEntries): a marking kept is one entry
/// on both sides, a marking changed is the old one taken off before the new one is put on, and nothing is written for
/// a zone that is unmarked on both sides.
/// </summary>
public class EditSessionResetEntriesTests
{
    private static readonly ZoneReset Keep = new(2, 3, true, false);
    private static readonly ZoneReset Clear = new(2, 3, false, true);

    [Fact]
    public void AZoneUnmarkedOnBothSidesWritesNothing()
    {
        Assert.Empty(EditSession.ResetEntries(null, null));
    }

    [Fact]
    public void AMarkingKeptAcrossTheStepIsOneEntryOnBothSides()
    {
        Assert.Equal([(Keep, true, true)], EditSession.ResetEntries(Keep, Keep));
    }

    [Fact]
    public void AMarkingTakenOffIsAnEntryOnlyBeforeTheStep()
    {
        Assert.Equal([(Keep, true, false)], EditSession.ResetEntries(Keep, null));
    }

    [Fact]
    public void AMarkingPutOnIsAnEntryOnlyAfterTheStep()
    {
        Assert.Equal([(Keep, false, true)], EditSession.ResetEntries(null, Keep));
    }

    [Fact]
    public void AMarkingChangedToOtherOptionsTakesTheOldOneOffThenPutsTheNewOneOn()
    {
        Assert.Equal([(Keep, true, false), (Clear, false, true)], EditSession.ResetEntries(Keep, Clear));
    }
}
