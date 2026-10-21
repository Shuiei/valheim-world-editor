using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;
using ValheimGen;

namespace TerrainEditor.Desktop;

/// <summary>
/// The world generator a new world is made with: the game's, or Riverheim when its switch is on and it is found (chosen
/// by default, as the game makes every new world a Riverheim world while the mod is installed). The choice shows only
/// when there is one.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>The generator chosen: the game's (0) or Riverheim (1).</summary>
    internal ComboBox GeneratorChoice { get; } = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 }.Tip("newWorld.generator");

    /// <summary>Riverheim's rules when it can lay new worlds out (kept while the game's generator is chosen).</summary>
    private RiverheimRules? _riverheim;

    /// <summary>Riverheim is chosen (kept apart from the list: the rules are read again away from the window's thread).</summary>
    private volatile bool _chooseRiverheim;

    /// <summary>Whether Riverheim is chosen (and there).</summary>
    internal bool RiverheimChosen => _riverheim != null && _chooseRiverheim;

    /// <summary>The choice's row: shown only when Riverheim can be chosen.</summary>
    private DockPanel GeneratorRow()
    {
        _riverheim = Rules.Riverheim;
        GeneratorChoice.ItemsSource = new[] { Lang.T("newWorld.generator.game"), Lang.T("newWorld.generator.riverheim", _riverheim?.Version ?? "") };
        _chooseRiverheim = _riverheim != null;
        GeneratorChoice.SelectedIndex = _chooseRiverheim ? 1 : 0;
        GeneratorChoice.SelectionChanged += (_, _) => ChooseGenerator();
        var row = Row(Lang.T("newWorld.label.generator"), GeneratorChoice);
        row.IsVisible = _riverheim != null;
        return row;
    }

    /// <summary>The rules with the generator chosen: Riverheim's added or taken away.</summary>
    /// <param name="rules">The rules as read.</param>
    private WorldRules WithChoice(WorldRules rules) =>
        rules.Riverheim == (RiverheimChosen ? _riverheim : null) ? rules : rules with { Riverheim = RiverheimChosen ? _riverheim : null };

    /// <summary>Another generator chosen: the rules follow, and the seed is looked at again with it.</summary>
    private void ChooseGenerator()
    {
        _chooseRiverheim = GeneratorChoice.SelectedIndex == 1;
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
        : Lang.T(rules.Riverheim != null ? "newWorld.hint.riverheim" : "newWorld.hint.expandWorld", ExpandWorld.Describe(rules));
}
