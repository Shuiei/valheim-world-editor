using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The Crypt and Ruins dungeons are made of the game's objects and hold.
/// </summary>
public class DungeonGenHoldsCryptAndRuinsTests
{
    [Theory]
    [MemberData(nameof(DungeonGenTests.SamplesOf), "Crypt", "Ruins", MemberType = typeof(DungeonGenTests))]
    public void ItIsMadeOfTheGamesObjectsAndHolds(string style, string biome, int size, int levels, int seed) =>
        DungeonGenTests.MadeOfTheGamesObjectsAndHolds(style, biome, size, levels, seed);
}
