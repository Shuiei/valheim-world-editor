using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>
    /// Regrow nature: the game's own vegetation for the zones under the selection (by its rules, on
    /// the ground as it is now), kept inside the selection, for the chosen kinds, and not where something
    /// already stands (a tree still there is not doubled) or near buildings.
    /// </summary>
    private async Task RegrowNature(EditSession session, List<Vector2> poly)
    {
        var s = session.Scene;
        float ox = s.OriginX, oz = s.OriginZ;
        // Taken once, when the spots are first judged (after the generator ran), not once per spot.
        var maskAt = new Lazy<Func<float, float, bool>>(MaskTest);
        var adds = await Growth.Spots(session, poly.Min(p => p.X) + ox, poly.Min(p => p.Y) + oz, poly.Max(p => p.X) + ox, poly.Max(p => p.Y) + oz,
            o => AreaTool.Inside(poly, o.X - ox, o.Z - oz) && Area.Kinds.Contains(ObjectKinds.Of(o.Name, false)) && maskAt.Value(o.X - ox, o.Z - oz), _nameOf, Say);
        if (adds == null || !StillOpen(session))
        {
            return;
        }
        if (adds.Count == 0)
        {
            Say(Said.Of("area.msg.nothingRegrow"));
            return;
        }
        Growth.Commit(session, Lang.T("area.undo.regrew", adds.Count), adds);
        var names = adds.Select(o => o.Name).Distinct().ToList();
        Say(Said.Of("area.msg.regrew", adds.Count, string.Join(", ", names.Take(6)) + (names.Count > 6 ? "…" : "")));
    }
}
