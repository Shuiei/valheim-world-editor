using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's rolls of a replayed dungeon part: a spawn rolls its chance, a pick draws one weighted choice, and a part whose
/// theme or elevation does not allow it is never shown.
/// </summary>
public class DungeonReplayRollTests
{
    [Fact]
    public void ASpawnThatRollsItsChanceShowsItsNodeAndHidesItsAlternate()
    {
        bool[] off = new bool[3];
        var spawns = new[] { new Dungeons.Spawn(Node: 0, Chance: 1000, Theme: 0, Off: 1, MinY: -1000, MaxY: 1000) };

        Dungeons.RollSpawns(spawns, 0, 0, off);

        Assert.False(off[0]);
        Assert.True(off[1]);
    }

    [Fact]
    public void ASpawnThatDoesNotRollHidesItsNode()
    {
        bool[] off = new bool[3];
        var spawns = new[] { new Dungeons.Spawn(Node: 0, Chance: -1, Theme: 0, Off: 1, MinY: -1000, MaxY: 1000) };

        Dungeons.RollSpawns(spawns, 0, 0, off);

        Assert.True(off[0]);
        Assert.False(off[1]);
    }

    [Fact]
    public void ASpawnOfAThemeTheDungeonDoesNotHaveDoesNotSpawn()
    {
        bool[] off = new bool[1];
        var spawns = new[] { new Dungeons.Spawn(Node: 0, Chance: 1000, Theme: 4, Off: -1, MinY: -1000, MaxY: 1000) };

        Dungeons.RollSpawns(spawns, 0, 0, off);

        Assert.True(off[0]);
    }

    [Fact]
    public void APickShowsExactlyOneOfItsChoices()
    {
        bool[] off = new bool[3];
        var picks = new[] { new Dungeons.Pick(Node: 0, Theme: 0, Choices: [(1, 1f), (2, 1f)], MinY: -1000, MaxY: 1000) };

        Dungeons.RollPicks(picks, 0, 0, off);

        Assert.False(off[0]);
        Assert.NotEqual(off[1], off[2]);
    }

    [Fact]
    public void APickOfAThemeTheDungeonDoesNotHaveHidesAllItsChoicesAndItsNode()
    {
        bool[] off = new bool[3];
        var picks = new[] { new Dungeons.Pick(Node: 0, Theme: 4, Choices: [(1, 1f), (2, 1f)], MinY: -1000, MaxY: 1000) };

        Dungeons.RollPicks(picks, 0, 0, off);

        Assert.Equal([true, true, true], off);
    }

    [Fact]
    public void ASingleWeightedChoiceIsAlwaysDrawn()
    {
        var pick = new Dungeons.Pick(Node: 0, Theme: 0, Choices: [(7, 2f)], MinY: 0, MaxY: 0);

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(7, Dungeons.RollChoice(pick));
        }
    }

    [Fact]
    public void AChoiceOfNoWeightIsNeverDrawn()
    {
        var pick = new Dungeons.Pick(Node: 0, Theme: 0, Choices: [(1, 0f), (2, 1f)], MinY: 0, MaxY: 0);

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(2, Dungeons.RollChoice(pick));
        }
    }

    [Fact]
    public void APickWithNoChoicesDrawsNothing()
    {
        var pick = new Dungeons.Pick(Node: 0, Theme: 0, Choices: [], MinY: 0, MaxY: 0);

        Assert.Null(Dungeons.RollChoice(pick));
    }

    [Fact]
    public void APartOfNoThemeIsAllowedAnywhereInItsElevationLimits()
    {
        Assert.True(Dungeons.Applies(theme: 0, minY: -5, maxY: 5, themes: 0, y: 0));
        Assert.False(Dungeons.Applies(theme: 0, minY: -5, maxY: 5, themes: 0, y: 6));
    }

    [Fact]
    public void APartOfAThemeIsAllowedOnlyWhenThatBitIsSet()
    {
        Assert.False(Dungeons.Applies(theme: 4, minY: -5, maxY: 5, themes: 0, y: 0));
        Assert.True(Dungeons.Applies(theme: 4, minY: -5, maxY: 5, themes: 4, y: 0));
    }
}
