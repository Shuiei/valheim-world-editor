using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// No limit ground's plan described in another language (the made-up one: PseudoLang): every clause (discs replaced,
/// trees and rocks taken away, ground still off, zones left out) is a catalogue text, and the English is the sentence it
/// always was.
/// </summary>
[Collection("DataDir")]
public sealed class UpliftLangTests
{
    [Fact]
    public void EveryClauseOfThePlanIsTranslatedAndTheEnglishKept()
    {
        using var pseudo = new PseudoLang();
        var plan = new Uplift.Plan([], [], [], Discs: 3, OldDiscs: 1, Cleared: 2, Worst: EditStore.MaxLevel + 4) { Ungenerated = 1 };
        var said = plan.Describe();
        Assert.Equal(
            "3 ground disc(s) (1 replaced), 2 tree(s)/rock(s) taken away; some ground is up to 4 m off (too steep); "
            + "1 zone(s) not generated yet were left as they were (visit them in game first)",
            said.English);
        Assert.Equal(
            PseudoLang.Wrap("3 ground disc(s) (1 replaced)") + ", " + PseudoLang.Wrap("2 tree(s)/rock(s) taken away") + "; "
            + PseudoLang.Wrap("some ground is up to 4 m off (too steep)") + "; "
            + PseudoLang.Wrap("1 zone(s) not generated yet were left as they were (visit them in game first)"),
            said.Shown);
        // Only what applies: no replaced discs, nothing taken away, nothing off, every zone generated.
        Assert.Equal("5 ground disc(s)", new Uplift.Plan([], [], [], 5, 0, 0, 0).Describe().English);
    }
}
