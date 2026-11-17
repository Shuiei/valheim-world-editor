using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Dungeon tool's panel: which dungeon of the area, going inside with its roofs cut away (and back down to its
/// entrance), the rooms it can take, and the changes (turn, delete, close the open ends). The roofs are cut by the
/// view's one cut (ViewCut, the status bar's slider), counted from the dungeon's floor while the tool is in use.
/// </summary>
public sealed partial class DungeonPanel
{
    /// <summary>The panel itself.</summary>
    public Control Card { get; }
    /// <summary>Generate a dungeon, at the top of the panel (open when the area has no dungeon).</summary>
    internal DungeonGenPanel Gen { get; } = new();
    /// <summary>The generator's expander.</summary>
    private readonly Expander _generate;
    /// <summary>The view: its dungeon tool and camera.</summary>
    private readonly GlView _view;
    /// <summary>The view's cut, which hides the dungeon's roofs.</summary>
    private readonly ViewCut _cut;
    /// <summary>The view's Dungeon tool the panel drives.</summary>
    private DungeonTool Tool => _view.Dungeon;
    /// <summary>Builds the panel for the view's Dungeon tool.</summary>
    /// <param name="view">The 3D view.</param>
    /// <param name="cut">The view's cut.</param>
    public DungeonPanel(GlView view, ViewCut cut)
    {
        _view = view;
        _cut = cut;
        Wire();
        (_body, _generate, _, Card) = BuildCard();
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
            CutFromFloor();
        }
        return true;
    }

    /// <summary>
    /// Whether the dungeon last placed in the area is built of pieces (no dungeon of rooms to list): the panel then says
    /// so, where it would say the area has no dungeon.
    /// </summary>
    internal void ShowPiecesPlaced(bool yes) => Empty.Text = yes ? Lang.T("dungeon.hint.piecesPlaced") : Lang.T("dungeon.hint.none");

    /// <summary>
    /// Leaving the tool: the view's cut counts from the ground again (the world's own cut, off unless it was on); the
    /// dungeon's is kept for when the tool is back.
    /// </summary>
    public void Leave() => _cut.FromGround();
}
