using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The builders the editor offers (Builders.Player): each choice's words name the player (or "Player id", or the unknown
/// player), whether the character is this computer's, and the pieces built; and a piece is claimed for a builder only
/// when it is a kind players build and has no builder yet (Builders.Claimed).
/// </summary>
public class BuildersPlayerTests
{
    [Fact]
    public void TheChoiceNamesThePlayerWhereItIsAndItsPieces()
    {
        Assert.Equal("Shuiei", new Builders.Player(5, "Shuiei", 0, false).Label);
        Assert.Equal("Unknown player · 1 piece", new Builders.Player(Builders.Unknown, null, 1, false).Label);
        Assert.Equal("Player 42 (this computer) · 3 pieces", new Builders.Player(42, null, 3, true).Label);
    }

    [Fact]
    public void OnlyAPieceWithoutABuilderIsClaimed()
    {
        const long Builder = 1234567890123L;
        byte[] wall = ZdoBuilder.Blank(StableHash.Of("woodwall"), 0, Vector3.Zero, Vector3.Zero, 1f);

        var claimed = Builders.Claimed(wall, Builder);

        Assert.NotNull(claimed);
        Assert.Equal(Builder, claimed.LongList.Single(l => l.Key == ObjectField.CreatorKey).Value);
        // Claimed already, or not a kind players build: nothing.
        Assert.Null(Builders.Claimed(claimed.Serialize(), 5));
        Assert.Null(Builders.Claimed(ZdoBuilder.Blank(StableHash.Of("Beech1"), 0, Vector3.Zero, Vector3.Zero, 1f), Builder));
    }
}
