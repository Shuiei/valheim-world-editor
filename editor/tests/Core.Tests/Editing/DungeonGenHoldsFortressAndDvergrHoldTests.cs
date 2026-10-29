using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The Fortress and Dvergr hold dungeons are made of the game's objects and hold.
/// </summary>
public class DungeonGenHoldsFortressAndDvergrHoldTests
{
    [Theory]
    [MemberData(nameof(DungeonGenTests.SamplesOf), "Fortress", "Dvergr hold", MemberType = typeof(DungeonGenTests))]
    public void ItIsMadeOfTheGamesObjectsAndHolds(string style, string biome, int size, int levels, int seed) =>
        DungeonGenTests.MadeOfTheGamesObjectsAndHolds(style, biome, size, levels, seed);
}
