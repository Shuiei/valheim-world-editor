using Avalonia.Controls;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Sets the controls' defaults and hooks their events.
/// </summary>
public sealed partial class DungeonGenPanel
{
    /// <summary>
    /// Sets the controls' defaults: the picks the panel opens on, the tooltips and the placeholder name.
    /// </summary>
    private void Defaults()
    {
        Biome.SelectedIndex = 1;
        Size.SelectedIndex = 1;
        Levels.SelectedIndex = 1;
        Light.SelectedIndex = 1;
        Decor.SelectedIndex = 1;
        Name.PlaceholderText = DungeonGen.NameFor(1, null, "Black Forest");
        Made.Tip("dungeon.gen.made");
        Biome.Tip("dungeon.gen.biome");
        Style.Tip("dungeon.gen.style");
        Walls.Tip("dungeon.gen.walls");
        Size.Tip("dungeon.gen.size");
        Levels.Tip("dungeon.gen.levels");
        Monsters.Tip("dungeon.gen.monsters");
        Respawn.Tip("dungeon.gen.respawn");
        Boss.Tip("dungeon.gen.boss");
        Loot.Tip("dungeon.gen.loot");
        Light.Tip("dungeon.gen.light");
        Decor.Tip("dungeon.gen.decor");
        Decay.Tip("dungeon.gen.decay");
        Loops.Tip("dungeon.gen.loops");
        BossKey.Tip("dungeon.gen.key");
        Secrets.Tip("dungeon.gen.secrets");
        Name.Tip("dungeon.gen.name");
        Seed.Tip("dungeon.gen.seed");
        Randomize.Tip("dungeon.gen.randomize");
        Place.Tip("dungeon.gen.place");
        ToWorkshop.Tip("dungeon.gen.workshop");
        OpenOnPlot.Tip("dungeon.gen.openOnPlot");
        AddToPlot.Tip("dungeon.gen.addToPlot");
        ToDungeon.Tip("dungeon.gen.toDungeon");
        ToEntrance.Tip("dungeon.gen.toEntrance");
    }

    /// <summary>
    /// Hooks the controls' events: a setting changed makes the dungeon again; the buttons raise the panel's events.
    /// </summary>
    private void Wire()
    {
        ToDungeon.Click += (_, _) => DungeonAsked?.Invoke();
        ToEntrance.Click += (_, _) => EntranceAsked?.Invoke();
        foreach (var c in new Control[] { Made, Biome, Style, Walls, Size, Levels, Boss, Light, Decor })
        {
            ((ComboBox)c).SelectionChanged += (_, _) => Changed();
        }
        foreach (var sl in new[] { Monsters, Loot, Decay, Loops })
        {
            sl.ValueChanged += (_, _) => Changed();
        }
        foreach (var cb in new[] { Respawn, BossKey, Secrets })
        {
            cb.IsCheckedChanged += (_, _) => Changed();
        }
        Name.TextChanged += (_, _) => Changed();
        Seed.TextChanged += (_, _) => Changed();
        // The style's own decay, until moved; with no style of its own (the biome's), the biome's style's, also when the
        // biome changes.
        void StyleDecay()
        {
            var st = DungeonKit.StyleOf(Style.SelectedIndex > 0 ? (string)Style.SelectedItem! : DungeonKit.BiomeOf((string?)Biome.SelectedItem).Style);
            Decay.Value = Math.Round(st.Decay / 0.05) * 0.05;
        }
        Style.SelectionChanged += (_, _) => StyleDecay();
        Biome.SelectionChanged += (_, _) =>
        {
            if (Style.SelectedIndex <= 0)
            {
                StyleDecay();
            }
        };
        Randomize.Click += (_, _) => Seed.Text = Random.Shared.Next(1, 1_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Place.Click += (_, _) =>
        {
            if (_result != null)
            {
                PlaceAsked?.Invoke(Settings(), _result);
            }
        };
        ToWorkshop.Click += (_, _) => WorkshopAsked?.Invoke(Settings() with { Made = DungeonGen.Made.Pieces });
        OpenOnPlot.Click += (_, _) => { if (_result != null) { PlotAsked?.Invoke(Settings(), _result, false); } };
        AddToPlot.Click += (_, _) => { if (_result != null) { PlotAsked?.Invoke(Settings(), _result, true); } };
    }
}
