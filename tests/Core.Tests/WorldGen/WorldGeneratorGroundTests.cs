using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The generator with a world generation mod's ground in place of the game's (Riverheim's, here a stand-in): its biome,
/// height, mask and forest answer for the game's bodies, given the point as Expand World Size's stretch leaves it, and
/// Expand World Size's depth and forest still apply to the answers; no water is laid out; the menu world keeps the game's.
/// </summary>
public class WorldGeneratorGroundTests
{
    /// <summary>A ground that answers from the point and remembers what it was asked.</summary>
    private sealed class Ground : IGroundGenerator
    {
        public List<string> Asked { get; } = [];

        public Heightmap.Biome Biome(float x, float z)
        {
            Asked.Add($"biome {x} {z}");
            return Heightmap.Biome.Plains;
        }

        public float Height(float x, float z, out Color mask)
        {
            Asked.Add($"height {x} {z}");
            mask = new Color(0.25f, 0.5f, 0f, 1f);
            return x / 100f;
        }

        public float Forest(float x, float z)
        {
            Asked.Add($"forest {x} {z}");
            return 1.5f;
        }
    }

    private static (WorldGenerator Generator, Ground Ground, List<int> Seeds) Make(WorldRules rules, bool menu = false)
    {
        var ground = new Ground();
        var seeds = new List<int>();
        var riverheim = new RiverheimRules("1.2.1", "builtin.amur", 2) { LayOut = seed => { seeds.Add(seed); return ground; } };
        var gen = WorldGenerator.Create(new World { Seed = 42, WorldGenVersion = 2, Menu = menu, Rules = rules with { Riverheim = riverheim } });
        return (gen, ground, seeds);
    }

    [Fact]
    public void ItsGroundAnswersForTheGamesAndNoWaterIsLaidOut()
    {
        var (gen, ground, seeds) = Make(new WorldRules());
        Assert.Equal([42], seeds);
        Assert.Empty(gen.GetLakes());
        Assert.Empty(gen.GetRivers());
        Assert.Empty(gen.GetStreams());
        Assert.Equal(Heightmap.Biome.Plains, gen.GetBiome(500f, 700f));
        Assert.Equal(5f, gen.GetBiomeHeight(Heightmap.Biome.Meadows, 500f, 700f, out var mask));
        Assert.Equal(new Color(0.25f, 0.5f, 0f, 1f), mask);
        Assert.Equal(1.5f, gen.GetForestFactor(new Vector3(500f, 0f, 700f)));
        Assert.Equal(5f, gen.GetAshlandsHeight(500f, 700f, out _));
        // Its GetHeight asks for the height with no biome: no biome is worked out.
        ground.Asked.Clear();
        Assert.Equal(5f, gen.GetHeight(500f, 700f));
        Assert.Equal(["height 500 700"], ground.Asked);
    }

    [Fact]
    public void TheStretchComesBeforeAndTheDepthAndForestAfter()
    {
        var (gen, ground, _) = Make(new WorldRules { Radius = 20000f, Stretch = 2f, WaterDepthMultiplier = 2f, ForestMultiplier = 3f });
        gen.GetBiome(1000f, 600f);
        // Under the water level (30 m): deeper by the multiplier around it.
        float h = gen.GetBiomeHeight(Heightmap.Biome.Plains, 1000f, 600f, out _);
        Assert.Equal((5f - 30f) * 2f + 30f, h);
        // The forest is asked at the point as given (no stretch on the game's GetForestFactor), then divided.
        Assert.Equal(0.5f, gen.GetForestFactor(new Vector3(1000f, 0f, 600f)));
        Assert.Equal(["biome 500 300", "height 500 300", "forest 1000 600"], ground.Asked);
    }

    [Fact]
    public void TheMenuWorldKeepsTheGamesGround()
    {
        var (gen, ground, seeds) = Make(new WorldRules(), menu: true);
        Assert.Empty(seeds);
        gen.GetBiome(500f, 700f);
        gen.GetHeight(500f, 700f);
        Assert.Empty(ground.Asked);
    }
}
