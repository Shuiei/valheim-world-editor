using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ValheimGen;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The New world page's generator choice: shown only when a world generation mod (here a made-up one) can lay new worlds
/// out, the mod's chosen at first, and the rules and the line saying what the worlds follow going with the choice.
/// </summary>
[Collection("DataDir")]
public sealed class NewWorldPageGeneratorTests
{
    /// <summary>A flat meadow, standing in for a world generation mod's ground.</summary>
    private sealed class Meadow : IGroundLayout
    {
        public int Biome(float x, float z) => (int)Heightmap.Biome.Meadows;

        public float Height(float x, float z, out System.Numerics.Vector4 mask)
        {
            mask = new System.Numerics.Vector4(0f, 0f, 0f, 1f);
            return 35f;
        }

        public float Forest(float x, float z) => 2f;
    }

    /// <summary>A made-up world generation mod's rules, laying worlds out slowly.</summary>
    private static readonly GeneratorRules Flatland = new("test.flatland", "Flatland", "2.0.0", "height 50") { Slow = true };

    [AvaloniaFact]
    public void TheGeneratorIsChosenOnlyWhenAModsIsThere()
    {
        var flatland = Flatland with { LayOut = _ => new Meadow() };
        using var page = new NewWorldPage([], previewSize: 16, rules: new WorldRules { Generator = flatland });
        Assert.True(((Control)page.GeneratorChoice.Parent!).IsVisible);
        Assert.Equal(1, page.GeneratorChoice.SelectedIndex);
        Assert.Same(flatland, page.Rules.Generator);
        Assert.Contains("Flatland 2.0.0 (height 50)", page.Generation.Text);
        // The game's generator: the rules are the game's own, and nothing is said.
        page.GeneratorChoice.SelectedIndex = 0;
        Assert.Null(page.Rules.Generator);
        Assert.True(page.Rules.IsVanilla);
        Assert.False(page.Generation.IsVisible);
        page.GeneratorChoice.SelectedIndex = 1;
        Assert.Same(flatland, page.Rules.Generator);
        Assert.True(page.Generation.IsVisible);
        using var plain = new NewWorldPage([], previewSize: 16, rules: WorldRules.Vanilla);
        Assert.False(((Control)plain.GeneratorChoice.Parent!).IsVisible);
    }

    [Fact]
    public void TheLineSaysWhatTheWorldsFollow()
    {
        Assert.Equal("", NewWorldPage.GenerationText(WorldRules.Vanilla));
        Assert.StartsWith("World generation: Expand World (", NewWorldPage.GenerationText(new WorldRules { Radius = 20000f }), StringComparison.Ordinal);
        Assert.Contains("Your Flatland lays each seed's world out", NewWorldPage.GenerationText(new WorldRules { Generator = Flatland }), StringComparison.Ordinal);
    }
}
