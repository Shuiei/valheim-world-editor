using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The panel's controls (the settings, the buttons, the summary and the plan's canvas) and the helpers that build them.
/// </summary>
public sealed partial class DungeonGenPanel
{
    /// <summary>Made of building pieces or of the game's own rooms (not shown in the Workshop).</summary>
    internal ComboBox Made { get; } = Combo(Lang.T("dungeon.made.pieces"), Lang.T("dungeon.made.rooms"));
    /// <summary>The biome (Black Forest at first).</summary>
    internal ComboBox Biome { get; } = Combo([.. DungeonKit.Biomes.Select(b => b.Name)]);
    /// <summary>The style, or the biome's.</summary>
    internal ComboBox Style { get; } = Combo([Lang.T("dungeon.biomesOwn"), .. DungeonKit.Styles.Select(s => s.Name)]);
    /// <summary>The wall material, or the biome's.</summary>
    internal ComboBox Walls { get; } = Combo([Lang.T("dungeon.biomesOwn"), .. DungeonKit.Materials.Select(m => m.Name)]);
    /// <summary>Rooms per level: Small to Huge (1 to 4).</summary>
    internal ComboBox Size { get; } = Combo(Lang.T("dungeon.size.small"), Lang.T("dungeon.size.medium"), Lang.T("dungeon.size.large"), Lang.T("dungeon.size.huge"));
    /// <summary>Levels, 1 to <see cref="DungeonGen.MaxLevels"/>.</summary>
    internal ComboBox Levels { get; } = Combo([.. Enumerable.Range(1, DungeonGen.MaxLevels).Select(n => n == 1 ? Lang.T("dungeon.levels.one") : Lang.T("dungeon.levels.many", n))]);
    /// <summary>Monsters, 0 (none) to 3 times the usual.</summary>
    internal Slider Monsters { get; } = Slide(0, 3, 1, 0.25);
    /// <summary>Monsters come back (spawners).</summary>
    internal CheckBox Respawn { get; } = new() { Content = Lang.T("dungeon.label.respawn"), IsChecked = true, FontSize = 12 };
    /// <summary>The boss: the biome's, none, or any biome's boss or elite.</summary>
    internal ComboBox Boss { get; } = Combo([Lang.T("dungeon.boss.biomes"), Lang.T("dungeon.boss.none"), .. Bosses]);
    /// <summary>Loot, 0 (none) to 2 times the usual.</summary>
    internal Slider Loot { get; } = Slide(0, 2, 1, 0.25);
    /// <summary>Light: dark, dim or bright.</summary>
    internal ComboBox Light { get; } = Combo(Lang.T("dungeon.light.dark"), Lang.T("dungeon.light.dim"), Lang.T("dungeon.light.bright"));
    /// <summary>Furniture: bare, furnished or rich.</summary>
    internal ComboBox Decor { get; } = Combo(Lang.T("dungeon.decor.bare"), Lang.T("dungeon.decor.furnished"), Lang.T("dungeon.decor.rich"));
    /// <summary>Ruin, 0 to 1 (set to the style's own when the style changes).</summary>
    internal Slider Decay { get; } = Slide(0, 1, 0.15, 0.05);
    /// <summary>Ways round, 0 (a tree) to 2 times the usual.</summary>
    internal Slider Loops { get; } = Slide(0, 2, 1, 0.25);
    /// <summary>Boss behind a gate, its key in a chest.</summary>
    internal CheckBox BossKey { get; } = new() { Content = Lang.T("dungeon.label.bossKey"), IsChecked = true, FontSize = 12 };
    /// <summary>Hidden caches.</summary>
    internal CheckBox Secrets { get; } = new() { Content = Lang.T("dungeon.label.secrets"), IsChecked = true, FontSize = 12 };
    /// <summary>The name on the entrance sign; empty: the made-up one shown as its placeholder.</summary>
    internal TextBox Name { get; } = new() { FontSize = 12 };
    /// <summary>The seed (a whole number; anything else counts as 0).</summary>
    internal TextBox Seed { get; } = new() { FontSize = 12, Width = 110, Text = "1" };

    /// <summary>Bosses and elites of every biome, by creature.</summary>
    private static string[] Bosses => [.. DungeonKit.Biomes.SelectMany(b => b.Elites.Append(b.Boss)).Select(f => f.Creature).Distinct()];

    /// <summary>A list of these entries, the first chosen.</summary>
    private static ComboBox Combo(params string[] items) => new() { ItemsSource = items, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };

    /// <summary>A slider that steps.</summary>
    private static Slider Slide(double min, double max, double value, double step) =>
        new() { Minimum = min, Maximum = max, Value = value, SmallChange = step, TickFrequency = step, IsSnapToTickEnabled = true };

    /// <summary>A labelled row.</summary>
    private static DockPanel Row(string label, Control c) => new()
    {
        Children = { new TextBlock { Text = label, FontSize = 12, Width = 80, VerticalAlignment = VerticalAlignment.Center }, c },
    };
}
