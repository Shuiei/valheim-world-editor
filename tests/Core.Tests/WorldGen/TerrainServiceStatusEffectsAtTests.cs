using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// <see cref="TerrainService.StatusEffectsAt"/> and <see cref="TerrainService.TerritoryAt"/>: the status effects
/// Expand World Data puts on a player at a point, a territory's in place of its biome's, a biome's only when the file
/// gives it settings.
/// </summary>
[Collection("World files")]
public class TerrainServiceStatusEffectsAtTests
{
    private static readonly StatusEffect Rested = new("Rested", true, true), Freezing = new("Freezing", false, true);

    /// <summary>Meadows everywhere, with Rested, and a territory 500 m round the middle (the effects given to it).</summary>
    private static TerrainService Terrain(TempWorld w, IReadOnlyList<StatusEffect> territoryEffects, bool biomeHasData = true)
    {
        var meadows = BiomeTable.Vanilla.Of(Heightmap.Biome.Meadows)! with { HasData = biomeHasData, StatusEffects = [Rested] };
        var world = w.Load();
        world.Rules = new WorldRules
        {
            DataInstalled = true,
            BiomeData = true,
            Biomes = new BiomeTable([.. BiomeTable.Vanilla.Biomes.Select(b => b.Value == Heightmap.Biome.Meadows ? meadows : b)]),
            World = [new BiomeRule { Territory = "cold", MaxDistance = 500f, WiggleDistanceWidth = 0f }, new BiomeRule { Biome = Heightmap.Biome.Meadows, SeedBiome = Heightmap.Biome.Meadows }],
            Territories = [new Territory { Name = "cold", StatusEffects = territoryEffects }],
        };
        return new TerrainService(world, null);
    }

    [Fact]
    public void ATerritorysEffectsReplaceItsBiomes()
    {
        using var w = new TempWorld();
        var terrain = Terrain(w, [Freezing]);
        Assert.Equal("cold", terrain.TerritoryAt(100f, 100f)?.Name);
        Assert.Equal([Freezing], terrain.StatusEffectsAt(100f, 100f));
        Assert.Null(terrain.TerritoryAt(2000f, 0f));
        Assert.Equal([Rested], terrain.StatusEffectsAt(2000f, 0f));
    }

    [Fact]
    public void ATerritoryWithoutEffectsKeepsItsBiomes()
    {
        using var w = new TempWorld();
        Assert.Equal([Rested], Terrain(w, []).StatusEffectsAt(100f, 100f));
    }

    [Fact]
    public void ABiomeWithoutSettingsHasNoEffect()
    {
        using var w = new TempWorld();
        Assert.Empty(Terrain(w, [], biomeHasData: false).StatusEffectsAt(2000f, 0f));
    }
}
