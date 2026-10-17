using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class PlaceInput
{
    /// <summary>Commits the placements a brush stroke made (the click's, or the drag's), as one undo step, and says so.</summary>
    /// <param name="session">The edit session the placements are committed to.</param>
    /// <param name="s">The finished stroke.</param>
    private void EndPlace(EditSession session, Stroke s)
    {
        var list = s.Dragging && !Tool.Building ? s.Added : s.Stamp;
        if (list.Count > 0)
        {
            var names = list.Select(o => o.Name).Distinct().ToList();
            Commit(session, list, Lang.T("place.undo.placed", list.Count, string.Join(", ", names.Take(3))));
            Placed?.Invoke(names);
        }
        Message?.Invoke(Tool.Building
            ? list.Count == 0 ? Said.Of("place.msg.nothingPlaced") : Built(string.Join(", ", list.Select(o => PieceCost.PieceName(o.Name)).Distinct()), Tool.Snapped)
            : Said.Of("place.msg.placedSave", list.Count));
    }

    /// <summary>What the status bar says when pieces were built: what, and what they snapped to.</summary>
    /// <param name="pieces">The pieces' names, joined.</param>
    /// <param name="snapped">What the last piece snapped to, if anything.</param>
    internal static Said Built(string pieces, (PlaceTool.SnapWays Way, string Name)? snapped)
    {
        return snapped switch
        {
            null => Said.Of("place.msg.built", pieces),
            { Way: PlaceTool.SnapWays.OnTop } s => Said.Of("place.msg.builtOnTop", pieces, s.Name),
            { Way: PlaceTool.SnapWays.Beside } s => Said.Of("place.msg.builtBeside", pieces, s.Name),
            { } s => Said.Of("place.msg.builtSnapped", pieces, s.Name),
        };
    }

    /// <summary>
    /// Commits placements as new objects in one undo step; kinds built with a tool go in as player-built pieces.
    /// </summary>
    private static void Commit(EditSession session, List<PlaceTool.Placement> list, string label)
    {
        var adds = list.Select(o =>
        {
            int prefab = StableHash.Of(o.Name);
            return (new NewObject(0, prefab, o.Position, o.Rotation, o.Scale), PieceCatalog.Get(prefab)?.Tool != null);
        }).ToList();
        session.Commit(label, null, [], adds);
    }
}
