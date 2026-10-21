using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using ValheimGen;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The New world page's generator choice: shown only when a world generation mod (Riverheim) can lay new worlds out, the
/// mod's chosen at first, and the rules and the line saying what the worlds follow going with the choice.
/// </summary>
[Collection("DataDir")]
public sealed class NewWorldPageGeneratorTests
{
    /// <summary>A flat meadow, standing in for Riverheim's ground.</summary>
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

    /// <summary>Riverheim 1.2.1's rules with its own preset, laying worlds out slowly as it does.</summary>
    private static readonly GeneratorRules Riverheim121 = new(Riverheim.PluginId, "Riverheim", "1.2.1", "builtin.amur v2") { Slow = true };

    [AvaloniaFact]
    public void TheGeneratorIsChosenOnlyWhenAModsIsThere()
    {
        var riverheim = Riverheim121 with { LayOut = _ => new Meadow() };
        using var page = new NewWorldPage([], previewSize: 16, rules: new WorldRules { Generator = riverheim });
        Assert.True(((Control)page.GeneratorChoice.Parent!).IsVisible);
        Assert.Equal(1, page.GeneratorChoice.SelectedIndex);
        Assert.Same(riverheim, page.Rules.Generator);
        Assert.Contains("Riverheim 1.2.1 (builtin.amur v2)", page.Generation.Text);
        // The game's generator: the rules are the game's own, and nothing is said.
        page.GeneratorChoice.SelectedIndex = 0;
        Assert.Null(page.Rules.Generator);
        Assert.True(page.Rules.IsVanilla);
        Assert.False(page.Generation.IsVisible);
        page.GeneratorChoice.SelectedIndex = 1;
        Assert.Same(riverheim, page.Rules.Generator);
        Assert.True(page.Generation.IsVisible);
        using var plain = new NewWorldPage([], previewSize: 16, rules: WorldRules.Vanilla);
        Assert.False(((Control)plain.GeneratorChoice.Parent!).IsVisible);
    }

    [Fact]
    public void TheLineSaysWhatTheWorldsFollow()
    {
        Assert.Equal("", NewWorldPage.GenerationText(WorldRules.Vanilla));
        Assert.StartsWith("World generation: Expand World (", NewWorldPage.GenerationText(new WorldRules { Radius = 20000f }), StringComparison.Ordinal);
        Assert.Contains("Your Riverheim lays each seed's world out", NewWorldPage.GenerationText(new WorldRules { Generator = Riverheim121 }), StringComparison.Ordinal);
    }
}
