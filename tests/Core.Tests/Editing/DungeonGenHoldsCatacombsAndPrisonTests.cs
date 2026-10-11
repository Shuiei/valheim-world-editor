using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The Catacombs and Prison dungeons are made of the game's objects and hold.
/// </summary>
public class DungeonGenHoldsCatacombsAndPrisonTests
{
    [Theory]
    [MemberData(nameof(DungeonGenTests.SamplesOf), "Catacombs", "Prison", MemberType = typeof(DungeonGenTests))]
    public void ItIsMadeOfTheGamesObjectsAndHolds(string style, string biome, int size, int levels, int seed) =>
        DungeonGenTests.MadeOfTheGamesObjectsAndHolds(style, biome, size, levels, seed);
}
