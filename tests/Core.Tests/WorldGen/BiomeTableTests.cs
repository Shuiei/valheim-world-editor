using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The biome table: the game's nine by default, names to biomes and back as Expand World Data reads and writes them,
/// a biome's terrain and lava, and the biome point map's indices under the game's mapping and the mod's.
/// </summary>
public class BiomeTableTests
{
    private static readonly Heightmap.Biome Custom = (Heightmap.Biome)0x400;

    /// <summary>The game's nine and one of the file's: "Lava Fields", shaped like the Ashlands, with lava.</summary>
    private static BiomeTable WithCustom() => new([.. BiomeTable.Vanilla.Biomes, new BiomeInfo { Value = Custom, Id = "Lava Fields", Terrain = Heightmap.Biome.AshLands, Nature = Heightmap.Biome.AshLands, Lava = true }]);

    [Fact]
    public void TheGamesTableHasItsNineBiomesUnchanged()
    {
        var v = BiomeTable.Vanilla;
        Assert.Equal(9, v.Biomes.Count);
        Assert.Equal("Meadows", v.Of(Heightmap.Biome.Meadows)!.Id);
        Assert.Equal(Heightmap.Biome.Swamp, v.Terrain(Heightmap.Biome.Swamp));
        Assert.Equal(Heightmap.Biome.Swamp, v.Nature(Heightmap.Biome.Swamp));
        Assert.Equal(Heightmap.Biome.AshLands, v.LavaBiomes);
        Assert.Equal(Heightmap.Biome.All, v.All);
        Assert.False(v.HasCustom);
        Assert.False(v.HasTerrainOverrides);
        Assert.True(v.Biomes.All(b => b.IsGames));
        Assert.Null(v.Of(Heightmap.Biome.None));
        Assert.Null(v.Of(Custom));
    }

    [Fact]
    public void NamesAreReadAsTheModReadsThem()
    {
        var v = BiomeTable.Vanilla;
        Assert.Equal(Heightmap.Biome.All, v.Parse(""));
        Assert.Equal(Heightmap.Biome.None, v.Parse("none"));
        Assert.Equal(Heightmap.Biome.Meadows | Heightmap.Biome.Swamp, v.Parse("meadows, SWAMP"));
        // A number is added as it is; a name that is neither is told and passed over.
        List<string> unknown = [];
        Assert.Equal(Heightmap.Biome.Plains | Heightmap.Biome.Meadows, v.Parse("16, Meadows, Nowhere", unknown.Add));
        Assert.Equal(["Nowhere"], unknown);
        Assert.True(v.TryGet("blackforest", out var forest));
        Assert.Equal(Heightmap.Biome.BlackForest, forest);
        Assert.False(v.TryGet("Lava Fields", out _));
    }

    [Fact]
    public void NamesAreWrittenAsTheModWritesThem()
    {
        var t = WithCustom();
        Assert.Equal("None", t.NameOf(Heightmap.Biome.None));
        Assert.Equal("Meadows, Swamp", t.NameOf(Heightmap.Biome.Meadows | Heightmap.Biome.Swamp));
        Assert.Equal("Lava Fields", t.NameOf(Custom));
        // A flag the table has no biome of: its number.
        Assert.Equal("Meadows, 2048", t.NameOf(Heightmap.Biome.Meadows | (Heightmap.Biome)0x800));
    }

    [Fact]
    public void ACustomBiomeBorrowsItsTerrainAndKeepsItsLava()
    {
        var t = WithCustom();
        Assert.True(t.HasCustom);
        Assert.True(t.HasTerrainOverrides);
        Assert.Equal(Heightmap.Biome.AshLands, t.Terrain(Custom));
        Assert.Equal(Heightmap.Biome.AshLands | Custom, t.LavaBiomes);
        Assert.Equal(Heightmap.Biome.All | Custom, t.All);
        Assert.True(t.TryGet("lava fields", out var found));
        Assert.Equal(Custom, found);
        // A flag with no entry is its own terrain.
        Assert.Equal((Heightmap.Biome)0x800, t.Terrain((Heightmap.Biome)0x800));
    }

    [Fact]
    public void TheGamesIndicesAreItsBiomeIndexOrder()
    {
        Assert.Equal(0, BiomeTable.IndexOf(Heightmap.Biome.None, false));
        Assert.Equal(1, BiomeTable.IndexOf(Heightmap.Biome.Meadows, false));
        Assert.Equal(6, BiomeTable.IndexOf(Heightmap.Biome.AshLands, false));
        Assert.Equal(8, BiomeTable.IndexOf(Heightmap.Biome.Ocean, false));
        Assert.Equal(9, BiomeTable.IndexOf(Heightmap.Biome.Mistlands, false));
        Assert.Equal(-1, BiomeTable.IndexOf(Custom, false));
        Assert.Equal(-1, BiomeTable.IndexOf(Heightmap.Biome.Meadows | Heightmap.Biome.Swamp, false));
        Assert.Equal(Heightmap.Biome.Mistlands, BiomeTable.AtIndex(9, false));
        Assert.Equal(Heightmap.Biome.None, BiomeTable.AtIndex(10, false));
    }

    [Fact]
    public void TheModsIndicesKeepTheGamesAndNumberEveryFlag()
    {
        foreach (var (value, _) in BiomeTable.GamesBiomes)
        {
            Assert.Equal(BiomeTable.IndexOf(value, false), BiomeTable.IndexOf(value, true));
        }
        Assert.Equal(10, BiomeTable.IndexOf((Heightmap.Biome)0x80, true));
        Assert.Equal(11, BiomeTable.IndexOf(Custom, true));
        Assert.Equal(32, BiomeTable.IndexOf(unchecked((Heightmap.Biome)0x80000000), true));
        Assert.Equal(-1, BiomeTable.IndexOf(Heightmap.Biome.Meadows | Heightmap.Biome.Swamp, true));
        for (int i = 0; i < BiomeTable.IndexCount; i++)
        {
            var biome = BiomeTable.AtIndex(i, true);
            Assert.Equal(i, BiomeTable.IndexOf(biome, true));
        }
        Assert.Equal(Heightmap.Biome.None, BiomeTable.AtIndex(BiomeTable.IndexCount, true));
    }
}
