using TerrainEditor.Terrain;
using ValheimGen;
using Xunit;
using Rnd = ValheimGen.UnityEngine.Random;

namespace WorldEditor.Tests;

/// <summary>
/// The draws a new object makes as it wakes (WakeDraws): its prefab's parts read from the game data, then made in order
/// on the random state replica: numbers, a piece's own (the caller's), a LOD fade, a snow melter in the Deep North only,
/// a container's default items unless its data says they were added.
/// </summary>
public class WakeDrawsTests
{
    /// <summary>The draws the parts make from seed 9 (by how many values the state moved on, up to 64).</summary>
    private static int Drawn(IReadOnlyList<WakeDraws.Part> parts, WakeDraws.Waking how)
    {
        Rnd.InitState(9);
        WakeDraws.Wake(parts, how);
        var after = Rnd.State;
        Rnd.InitState(9);
        for (int n = 0; n <= 64; n++)
        {
            if (Rnd.State.Equals(after))
            {
                return n;
            }
            _ = Rnd.Value;
        }
        throw new InvalidOperationException("more than 64 draws");
    }

    /// <summary>A prefab's parts from its JSON list.</summary>
    private static IReadOnlyList<WakeDraws.Part> Parts(string list) => WakeDraws.Read($$"""{"p":{{list}}}""")["p"];

    /// <summary>A wake whose piece draws two.</summary>
    private static readonly WakeDraws.Waking Piece = new(() => WakeDraws.Skip(2));

    [Fact]
    public void EachPartIsReadAsWritten()
    {
        var parts = Parts("""[3,"wear","lod","melt",{"chance":0.5,"min":1,"max":2,"each":true,"weights":[1.0,0.25]}]""");
        Assert.Equal([WakeDraws.PartKind.Draws, WakeDraws.PartKind.Wear, WakeDraws.PartKind.Lod, WakeDraws.PartKind.Melt, WakeDraws.PartKind.Loot], parts.Select(p => p.Kind));
        Assert.Equal(3, parts[0].Count);
        var loot = parts[4].Loot!;
        Assert.Equal((0.5f, 1, 2, true), (loot.Chance, loot.Min, loot.Max, loot.Each));
        Assert.Equal([1f, 0.25f], loot.Weights);
        Assert.Throws<InvalidDataException>(() => Parts("""["fly"]"""));
    }

    [Fact]
    public void NumbersAPieceAndALodFadeDrawInOrder()
    {
        int pieces = 0;
        Assert.Equal(6, Drawn(Parts("""[3,"wear","lod"]"""), new(() =>
        {
            pieces++;
            WakeDraws.Skip(2);
        })));
        Assert.Equal(1, pieces);
    }

    [Fact]
    public void ASnowMelterDrawsInTheDeepNorthOnly()
    {
        Assert.Equal(0, Drawn(Parts("""["melt"]"""), Piece));
        Assert.Equal(1, Drawn(Parts("""["melt"]"""), Piece with { DeepNorth = true }));
    }

    [Fact]
    public void AContainersDefaultItemsDrawByTheirRolls()
    {
        // No chance: the chance's draw only.
        Assert.Equal(1, Drawn(Parts("""[{"chance":0.0,"min":2,"max":2,"each":false,"weights":[1.0]}]"""), Piece));
        // Two picks of one item: the chance, the count, then a roll and a stack each.
        Assert.Equal(6, Drawn(Parts("""[{"chance":1.0,"min":2,"max":2,"each":false,"weights":[1.0]}]"""), Piece));
        // Once each: the second roll finds nothing left, so no stack.
        Assert.Equal(5, Drawn(Parts("""[{"chance":1.0,"min":2,"max":2,"each":true,"weights":[1.0]}]"""), Piece));
        // Its data says they were added: nothing drawn.
        Assert.Equal(0, Drawn(Parts("""[{"chance":1.0,"min":2,"max":2,"each":false,"weights":[1.0]}]"""), Piece with { Looted = true }));
    }

    [Fact]
    public void APartOfNoKnownKindIsRefused() =>
        Assert.Throws<InvalidDataException>(() => WakeDraws.Wake([new WakeDraws.Part((WakeDraws.PartKind)99)], Piece));

    [Fact]
    public void DataSaysWhenTheDefaultItemsWereAdded()
    {
        int key = TerrainEditor.Save.StableHash.Of("addedDefaultItems");
        Assert.False(WakeDraws.Looted(null));
        Assert.False(WakeDraws.Looted([new ContentField("ints", TerrainEditor.Save.StableHash.Of("other"), "1")]));
        Assert.True(WakeDraws.Looted([new ContentField("ints", key, "1")]));
        // The last value wins, as it does when the data is written.
        Assert.False(WakeDraws.Looted([new ContentField("ints", key, "1"), new ContentField("ints", key, "0")]));
    }

    [Fact]
    public void APrefabOfTheGameDataDrawsItsParts()
    {
        // The tests' copy: a chest's default items then its piece; a prefab that draws nothing, or unknown, has none.
        Assert.Equal([WakeDraws.PartKind.Loot, WakeDraws.PartKind.Wear], WakeDraws.Of("TreasureChest_meadows").Select(p => p.Kind));
        Assert.Empty(WakeDraws.Of("Bush01"));
        Assert.Empty(WakeDraws.Of("NoSuchPrefab"));
    }
}
