using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The settings shown as one dungeon's settings, and what a change of them shows and starts.
/// </summary>
public sealed partial class DungeonGenPanel
{
    /// <summary>The settings shown.</summary>
    public DungeonGen.Settings Settings()
    {
        int.TryParse(Seed.Text?.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int seed);
        string? boss = Boss.SelectedIndex switch { 0 => null, 1 => "", _ => (string)Boss.SelectedItem! };
        return new DungeonGen.Settings(
            Made: Made.SelectedIndex == 1 ? DungeonGen.Made.Rooms : DungeonGen.Made.Pieces,
            Biome: (string?)Biome.SelectedItem ?? "Black Forest",
            Style: Style.SelectedIndex > 0 ? (string)Style.SelectedItem! : null,
            Walls: Walls.SelectedIndex > 0 ? (string)Walls.SelectedItem! : null,
            Size: Size.SelectedIndex + 1, Levels: Levels.SelectedIndex + 1,
            Monsters: (float)Monsters.Value, Respawn: Respawn.IsChecked == true, Boss: boss,
            Loot: (float)Loot.Value, Light: Light.SelectedIndex, Decor: Decor.SelectedIndex, Decay: (float)Decay.Value, Loops: (float)Loops.Value,
            BossKey: BossKey.IsChecked == true, Secrets: Secrets.IsChecked == true, Seed: seed,
            Name: string.IsNullOrWhiteSpace(Name.Text) ? null : Name.Text.Trim());
    }

    /// <summary>A setting changed: the labels follow, and the dungeon is made again after a pause.</summary>
    private void Changed()
    {
        var s = Settings();
        bool pieces = s.Made == DungeonGen.Made.Pieces;
        foreach (var c in _piecesOnly)
        {
            c.IsVisible = pieces;
        }
        _monstersText.Text = s.Monsters == 0 ? Lang.T("dungeon.label.monstersNone") : Lang.T("dungeon.label.monsters", s.Monsters);
        _lootText.Text = s.Loot == 0 ? Lang.T("dungeon.label.lootNone") : Lang.T("dungeon.label.loot", s.Loot);
        _decayText.Text = Lang.T("dungeon.label.ruin", s.Decay * 100);
        _loopsText.Text = s.Loops == 0 ? Lang.T("dungeon.label.waysNone") : Lang.T("dungeon.label.ways", s.Loops);
        // The game's rooms are named after the biome alone (the style, hidden then, is the pieces' own).
        Name.PlaceholderText = DungeonGen.NameFor(s.Seed, pieces ? s.Style : null, s.Biome);
        Place.IsEnabled = OpenOnPlot.IsEnabled = AddToPlot.IsEnabled = false;
        // A dungeon still being made for the settings before is not shown when it is done: it would come
        // back as the one to place, made for other settings.
        _generation++;
        _wait.Stop();
        _wait.Start();
    }
}
