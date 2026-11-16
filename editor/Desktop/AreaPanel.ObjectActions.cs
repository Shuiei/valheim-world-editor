using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>
    /// Replaces things with objects of another kind, where they are and turned as they are (the Select tool's Replace
    /// the selection). A linked group's root takes its parts away with it (ObjectLinks: a ship): they would have
    /// nothing to hang on.
    /// </summary>
    /// <param name="session">The session the change is committed to (one undo step).</param>
    /// <param name="things">The things, as indices into the scene's things.</param>
    /// <param name="prefab">The prefab hash of the new kind.</param>
    public void Replace(EditSession session, IReadOnlyList<int> things, int prefab)
    {
        var s = session.Scene;
        bool piece = PieceCatalog.Get(prefab)?.Tool != null;
        var adds = things.Select(i => s.Things[i]).Select(t => (new NewObject(0, prefab, t.Position, t.Rotation, 0), piece)).ToList();
        var (removed, linked, groups, _) = session.WithLinked(things, anyPart: false);
        // The groups' parts, roots left out (a part among the things given counts: it goes as a part).
        int parts = linked.Count - groups;
        session.Commit(Lang.T("area.undo.replaced", things.Count, NameOf(prefab)), null, removed, adds);
        Say(Said.Of("area.msg.replaced", things.Count, NameOf(prefab)) + (parts > 0 ? Said.Of("area.msg.linkedParts", parts) : Said.Empty));
    }

    /// <summary>
    /// Select objects: selects the objects of the chosen kinds inside the selection, where the Mask lets the ground
    /// through (only one kind of object among them when the Only list names one), and hands them to the Select tool:
    /// Delete removes them there, Replace with turns them into another kind.
    /// </summary>
    /// <param name="poly">The selection, in metres from the scene's corner.</param>
    /// <returns>False when no such object is inside (it says so, and the Region tool stays).</returns>
    private bool SelectInside(List<Vector2> poly)
    {
        var inside = ThingsInside(poly, chosenKindsOnly: true);
        if (OnlyName() is string only && _view.Scene is { } s)
        {
            inside = [.. inside.Where(i => NameOf(s.Things[i].Prefab) == only)];
        }
        if (inside.Count == 0)
        {
            Say(Said.Of("area.msg.noObjects"));
            return false;
        }
        _view.Select(inside);
        SwitchToSelect?.Invoke();
        Say(Said.Of("area.msg.selected", inside.Count));
        return true;
    }
}
