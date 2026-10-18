using ValheimGen;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A pasted dungeon's biome: the ground's biome named as the dungeon generator knows it, the Black Forest standing in
/// for no biome at all.
/// </summary>
public sealed class MainWindowDungeonPasteTests
{
    [Theory]
    [InlineData(Heightmap.Biome.Meadows, "Meadows")]
    [InlineData(Heightmap.Biome.Swamp, "Swamp")]
    [InlineData(Heightmap.Biome.Mountain, "Mountain")]
    [InlineData(Heightmap.Biome.Plains, "Plains")]
    [InlineData(Heightmap.Biome.Mistlands, "Mistlands")]
    [InlineData(Heightmap.Biome.AshLands, "Ashlands")]
    [InlineData(Heightmap.Biome.DeepNorth, "Deep North")]
    [InlineData(Heightmap.Biome.BlackForest, "Black Forest")]
    [InlineData(Heightmap.Biome.None, "Black Forest")]
    [InlineData(null, "Black Forest")]
    public void TheGroundsBiomeIsNamedAsTheDungeonGeneratorKnowsIt(Heightmap.Biome? biome, string name)
    {
        Assert.Equal(name, MainWindow.BiomeName(biome));
    }
}
