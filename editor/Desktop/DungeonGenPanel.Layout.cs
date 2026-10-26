using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The panel's layout: the settings, the plan and the summary in one column, and the actions where the host puts them.
/// </summary>
public sealed partial class DungeonGenPanel
{
    /// <summary>
    /// Lays the controls out: the settings, the plan and the summary, and the actions (in the Workshop, the host puts them).
    /// </summary>
    /// <param name="inWorkshop">In the Workshop: building pieces only, and Open and Add to the plot in the actions.</param>
    /// <returns>The view and the actions row.</returns>
    private (Control View, Control Actions) BuildView(bool inWorkshop)
    {
        // Loot is between rows of the pieces' own, and the game's rooms take it too (their chests): it stays shown.
        var pieces = new StackPanel
        {
            Spacing = 6,
            Children = { Row(Lang.T("dungeon.label.style"), Style), Row(Lang.T("dungeon.label.walls"), Walls), Row(Lang.T("dungeon.label.levels"), Levels) },
        };
        var piecesAfterLoot = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Row(Lang.T("dungeon.label.light"), Light), Row(Lang.T("dungeon.label.furniture"), Decor),
                new StackPanel { Spacing = 0, Children = { _decayText, Decay } },
                new StackPanel { Spacing = 0, Children = { _loopsText, Loops } },
                Row(Lang.T("dungeon.label.boss"), Boss), BossKey, Secrets, Row(Lang.T("dialog.label.name"), Name),
            },
        };
        var plan = new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Child = _plan };
        // The game's rooms have no spawners to turn into monsters once, and no plan to draw.
        _piecesOnly.AddRange([Respawn, pieces, piecesAfterLoot, plan, ToWorkshop]);
        // The plot only takes building pieces (the game's rooms are kept in a world's dungeon).
        var made = Row(Lang.T("dungeon.label.madeOf"), Made);
        made.IsVisible = !inWorkshop;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.AddRange(inWorkshop ? new Control[] { OpenOnPlot, AddToPlot } : [Place, ToWorkshop]);
        var view = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                made, Row(Lang.T("dungeon.label.biome"), Biome), Row(Lang.T("dungeon.label.size"), Size),
                new StackPanel { Spacing = 0, Children = { _monstersText, Monsters } }, Respawn,
                pieces, new StackPanel { Spacing = 0, Children = { _lootText, Loot } }, piecesAfterLoot,
                new DockPanel { Children = { new TextBlock { Text = Lang.T("common.seed"), FontSize = 12, Width = 80, VerticalAlignment = VerticalAlignment.Center }, Randomize, Seed } },
                plan, _levelButtons, Summary,
            },
        };
        if (!inWorkshop)
        {
            view.Children.Add(actions);
        }
        return (view, actions);
    }
}
