using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// What the panel says about the dungeon edited: its rooms, open ends, overlaps and problems.
/// </summary>
public sealed partial class DungeonPanel
{
    /// <summary>Fills <see cref="Info"/> and enables the buttons that apply.</summary>
    private void Refresh()
    {
        if (Tool.Dungeon is not { } d)
        {
            Info.Text = "";
            return;
        }
        int open = Tool.FreeEnds.Count;
        int overlaps = Tool.OverlappingPairs;
        var text = Said.Of("dungeon.hint.count", d.Rooms.Count, open);
        if (overlaps > 0)
        {
            text += Said.Of("dungeon.hint.overlaps", overlaps);
        }
        if (d.Problem is { } why)
        {
            text += why;
        }
        if (Tool.Preview is { } p && Tool.Problem(p) is { } problem)
        {
            text += Said.Of("dungeon.hint.thisOne", problem);
        }
        Info.Text = text.Shown;
        DeleteButton.IsEnabled = Tool.Selected != null;
        CloseEnds.IsEnabled = open > 0;
    }
}
