using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// The support check over the plot's building pieces (Stability), shown as coloured outlines; pieces that would
    /// fall in pink.
    /// </summary>
    internal void RefreshSupport()
    {
        if (!InWorkshop || SupportBox.IsChecked != true || Session is not { } s)
        {
            LastSupport = null;
            View.ShowSupport(null);
            return;
        }
        var (r, index) = SolveSupport(s);
        LastSupport = r;
        LastSupportThings = index;
        var show = new Dictionary<int, float>();
        for (int k = 0; k < index.Count; k++)
        {
            if (!r.Free[k])
            {
                show[index[k]] = r.Breaks[k] ? -2 : r.Colour[k];
            }
        }
        View.ShowSupport(show);
    }

    /// <summary>
    /// The support check over the plot's building pieces (Stability), whether or not its colours are shown.
    /// </summary>
    /// <returns>The result, and the scene's index of each piece it was worked out for.</returns>
    internal (Stability.Result Result, List<int> Index) SolveSupport(EditSession s)
    {
        var index = new List<int>();
        var pieces = new List<Stability.Piece>();
        lock (s.Scene.Things)
        {
            for (int i = 0; i < s.Scene.Things.Count; i++)
            {
                var t = s.Scene.Things[i];
                // Buildable pieces only (rocks and other things the hoe places are not the building).
                if (t.Gone || PrefabCatalog.NameOf(t.Prefab) is not string name || !Workshop.Buildable(name))
                {
                    continue;
                }
                index.Add(i);
                pieces.Add(new Stability.Piece(name, t.Position, BlueprintFormats.FromEuler(t.Rotation), t.Scale > 0 ? t.Scale : 1));
            }
        }
        var terrain = _workshopTerrain;
        return (Stability.Solve(pieces, (x, z) => Workshop.GroundAt(terrain, x, z)), index);
    }

    /// <summary>
    /// What the building on the plot costs to build in game, under the title (all of it in the tip).
    /// </summary>
    internal string WorkshopCost { get; private set; } = "";
}
