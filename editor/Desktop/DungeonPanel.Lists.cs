using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The area's dungeons and the rooms the dungeon can take, listed for the panel's lists.
/// </summary>
public sealed partial class DungeonPanel
{
    /// <summary>The area's dungeons again (on entering the tool, and after an edit or undo).</summary>
    public void Reload()
    {
        _filling = true;
        _shown = [.. Tool.All.Where(d => d.Kind.Interior || d.Rooms.Count > 0)];
        DungeonBox.ItemsSource = _shown.Select(DungeonTool.Describe).ToList();
        int i = Tool.Current is { } at ? _shown.FindIndex(d => System.Numerics.Vector3.DistanceSquared(d.Thing.Position, at) < 0.01f) : -1;
        if (i < 0 && _shown.Count > 0)
        {
            i = 0;
            Tool.Open(_shown[0]);
        }
        DungeonBox.SelectedIndex = i;
        _filling = false;
        Empty.IsVisible = _shown.Count == 0;
        _body.IsVisible = _shown.Count > 0;
        _generate.IsExpanded = _shown.Count == 0;
        // Made before the game's data was read (nothing to place): made again now.
        if (Gen.Failed)
        {
            Gen.Remake();
        }
        FillRooms();
        Refresh();
        // Back from Build here or another tool: the cut the slider shows.
        SetCut(CutSlider.Value);
    }

    /// <summary>Lists the rooms the dungeon can take that match the filter, the one chosen selected.</summary>
    private void FillRooms()
    {
        _filling = true;
        string f = Filter.Text?.Trim() ?? "";
        var items = Tool.Palette.Where(r => f.Length == 0 || r.Name.Contains(f, StringComparison.OrdinalIgnoreCase))
            .Select(r => new ListBoxItem
            {
                Content = r.EndCap ? Lang.T("dungeon.label.endCapRoom", r.Name) : r.Entrance ? Lang.T("dungeon.label.entranceRoom", r.Name) : r.Name,
                Tag = r.Name,
            }).ToList();
        Rooms.ItemsSource = items;
        Rooms.SelectedItem = items.FirstOrDefault(i => (string?)i.Tag == Tool.Chosen);
        _filling = false;
    }
}
