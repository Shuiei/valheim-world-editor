using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;
using ValheimGen;

namespace TerrainEditor.Desktop;

/// <summary>
/// The world generator a new world is made with: the game's, or a world generation mod's when one in use makes new worlds
/// (WorldGenerators.ForNewWorld; chosen by default, as the game makes every new world with the mod while it is
/// installed). The choice shows only when there is one.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>The generator chosen: the game's (0) or the mod's (1).</summary>
    internal ComboBox GeneratorChoice { get; } = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 }.Tip("newWorld.generator");

    /// <summary>The mod's generator rules when one can lay new worlds out (kept while the game's generator is chosen).</summary>
    private GeneratorRules? _generator;

    /// <summary>The mod's generator is chosen (kept apart from the list: the rules are read again away from the window's thread).</summary>
    private volatile bool _chooseMod;

    /// <summary>Whether the mod's generator is chosen (and there).</summary>
    internal bool ModGeneratorChosen => _generator != null && _chooseMod;

    /// <summary>The choice's row: shown only when a mod's generator can be chosen.</summary>
    private DockPanel GeneratorRow()
    {
        _generator = Rules.Generator;
        GeneratorChoice.ItemsSource = new[] { Lang.T("newWorld.generator.game"), Lang.T("newWorld.generator.mod", _generator?.Name ?? "", _generator?.Version ?? "") };
        _chooseMod = _generator != null;
        GeneratorChoice.SelectedIndex = _chooseMod ? 1 : 0;
        GeneratorChoice.SelectionChanged += (_, _) => ChooseGenerator();
        var row = Row(Lang.T("newWorld.label.generator"), GeneratorChoice);
        row.IsVisible = _generator != null;
        return row;
    }

    /// <summary>The rules with the generator chosen: the mod's added or taken away.</summary>
    /// <param name="rules">The rules as read.</param>
    private WorldRules WithChoice(WorldRules rules) =>
        rules.Generator == (ModGeneratorChosen ? _generator : null) ? rules : rules with { Generator = ModGeneratorChosen ? _generator : null };

    /// <summary>Another generator chosen: the rules follow, and the seed is looked at again with it.</summary>
    private void ChooseGenerator()
    {
        _chooseMod = GeneratorChoice.SelectedIndex == 1;
        Rules = WithChoice(Rules);
        ShowGeneration();
        ShowSeed((SeedBox.Text ?? "").Trim());
    }

    /// <summary>The line saying what the worlds follow, shown unless they are the game's own.</summary>
    private void ShowGeneration()
    {
        Generation.Text = GenerationText(Rules);
        Generation.IsVisible = !Rules.IsVanilla;
    }

    /// <summary>What worlds made with these rules follow, for the page; empty for the game's own.</summary>
    /// <param name="rules">The rules.</param>
    internal static string GenerationText(WorldRules rules) => rules.IsVanilla ? ""
        : rules.Generator is { Slow: true } g ? Lang.T("newWorld.hint.generator", WorldRulesText.Describe(rules), g.Name)
        : Lang.T("newWorld.hint.expandWorld", WorldRulesText.Describe(rules));
}
