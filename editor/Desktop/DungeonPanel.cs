using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Dungeon tool's panel: which dungeon of the area, going inside and cutting its roofs away (and back
/// down to its entrance), the rooms it can take, and the changes (turn, delete, close the open ends).
/// </summary>
public sealed partial class DungeonPanel
{
    /// <summary>The panel itself.</summary>
    public Control Card { get; }
    /// <summary>Generate a dungeon, at the top of the panel (open when the area has no dungeon).</summary>
    internal DungeonGenPanel Gen { get; } = new();
    /// <summary>The generator's expander.</summary>
    private readonly Expander _generate;
    /// <summary>The view: its dungeon tool, camera and roof cut.</summary>
    private readonly GlView _view;
    /// <summary>The view's Dungeon tool the panel drives.</summary>
    private DungeonTool Tool => _view.Dungeon;
    /// <summary>Builds the panel for the view's Dungeon tool.</summary>
    /// <param name="view">The 3D view.</param>
    public DungeonPanel(GlView view)
    {
        _view = view;
        Wire();
        (_body, _generate, _, Card) = BuildCard();
        SetCut(CutOff);
    }

    /// <summary>
    /// Makes the dungeon whose object stands at <paramref name="at"/> the one edited (listed first when it was not
    /// listed yet); false when the area has none there.
    /// </summary>
    internal bool Select(System.Numerics.Vector3 at)
    {
        if (!_shown.Any(d => System.Numerics.Vector3.DistanceSquared(d.Thing.Position, at) < 0.01f))
        {
            Reload();
        }
        int i = _shown.FindIndex(d => System.Numerics.Vector3.DistanceSquared(d.Thing.Position, at) < 0.01f);
        if (i < 0)
        {
            return false;
        }
        DungeonBox.SelectedIndex = i;
        if (Tool.Dungeon is not { } d || System.Numerics.Vector3.DistanceSquared(d.Thing.Position, at) >= 0.01f)
        {
            Tool.Open(_shown[i]);
            FillRooms();
        }
        return true;
    }

    /// <summary>
    /// Whether the dungeon last placed in the area is built of pieces (no dungeon of rooms to list): the panel then says
    /// so, where it would say the area has no dungeon.
    /// </summary>
    internal void ShowPiecesPlaced(bool yes) => Empty.Text = yes ? Lang.T("dungeon.hint.piecesPlaced") : Lang.T("dungeon.hint.none");

    /// <summary>Leaving the tool: the cut off.</summary>
    public void Leave() => _view.CutY = null;
}
