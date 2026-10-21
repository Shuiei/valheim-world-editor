using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ValheimGen;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The New world page's generator choice: shown only when Riverheim can lay new worlds out, Riverheim chosen at first,
/// and the rules and the line saying what the worlds follow going with the choice.
/// </summary>
[Collection("DataDir")]
public sealed class NewWorldPageGeneratorTests
{
    /// <summary>A flat meadow, standing in for Riverheim's ground.</summary>
    private sealed class Meadow : IGroundGenerator
    {
        public Heightmap.Biome Biome(float x, float z) => Heightmap.Biome.Meadows;

        public float Height(float x, float z, out Color mask)
        {
            mask = Color.Black;
            return 35f;
        }

        public float Forest(float x, float z) => 2f;
    }

    [AvaloniaFact]
    public void TheGeneratorIsChosenOnlyWhenRiverheimIsThere()
    {
        var riverheim = new RiverheimRules("1.2.1", "builtin.amur", 2) { LayOut = _ => new Meadow() };
        using var page = new NewWorldPage([], previewSize: 16, rules: new WorldRules { Riverheim = riverheim });
        Assert.True(((Control)page.GeneratorChoice.Parent!).IsVisible);
        Assert.Equal(1, page.GeneratorChoice.SelectedIndex);
        Assert.Same(riverheim, page.Rules.Riverheim);
        Assert.Contains("Riverheim 1.2.1 (builtin.amur v2)", page.Generation.Text);
        // The game's generator: the rules are the game's own, and nothing is said.
        page.GeneratorChoice.SelectedIndex = 0;
        Assert.Null(page.Rules.Riverheim);
        Assert.True(page.Rules.IsVanilla);
        Assert.False(page.Generation.IsVisible);
        page.GeneratorChoice.SelectedIndex = 1;
        Assert.Same(riverheim, page.Rules.Riverheim);
        Assert.True(page.Generation.IsVisible);
        using var plain = new NewWorldPage([], previewSize: 16, rules: WorldRules.Vanilla);
        Assert.False(((Control)plain.GeneratorChoice.Parent!).IsVisible);
    }

    [Fact]
    public void TheLineSaysWhatTheWorldsFollow()
    {
        Assert.Equal("", NewWorldPage.GenerationText(WorldRules.Vanilla));
        Assert.StartsWith("World generation: Expand World (", NewWorldPage.GenerationText(new WorldRules { Radius = 20000f }), StringComparison.Ordinal);
        Assert.Contains("Your Riverheim lays each seed's world out", NewWorldPage.GenerationText(new WorldRules { Riverheim = new RiverheimRules("1.2.1", "builtin.amur", 2) }), StringComparison.Ordinal);
    }
}
