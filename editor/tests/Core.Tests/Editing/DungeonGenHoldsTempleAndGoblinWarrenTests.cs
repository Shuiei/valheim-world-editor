using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The Temple and Goblin warren dungeons are made of the game's objects and hold.
/// </summary>
public class DungeonGenHoldsTempleAndGoblinWarrenTests
{
    [Theory]
    [MemberData(nameof(DungeonGenTests.SamplesOf), "Temple", "Goblin warren", MemberType = typeof(DungeonGenTests))]
    public void ItIsMadeOfTheGamesObjectsAndHolds(string style, string biome, int size, int levels, int seed) =>
        DungeonGenTests.MadeOfTheGamesObjectsAndHolds(style, biome, size, levels, seed);
}
