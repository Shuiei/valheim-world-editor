using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>The eyedropper's kind into the "with" list, when the game can make it.</summary>
    internal void UsePicked(int prefab)
    {
        FillCreatable();
        int i = _creatable.IndexOf(prefab);
        if (i < 0)
        {
            Say(Said.Of("select.msg.cannotPlace", NameOf(prefab)));
            return;
        }
        ToBox.SelectedIndex = i;
        Say(Said.Of("select.msg.replaceWith", NameOf(prefab)));
    }

    /// <summary>
    /// Replaces things with objects of another kind, where they are and turned as they are. A linked group's root
    /// takes its parts away with it (ObjectLinks: a ship), as Remove does: they would have nothing to hang on.
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

    /// <summary>Removes the objects of the chosen kinds inside the selection, the parts of a linked group with their root, as one undo step.</summary>
    /// <param name="session">The session the removal is committed to.</param>
    /// <param name="poly">The selection, in metres from the scene's corner.</param>
    /// <returns>False when no object is inside (it says so).</returns>
    private bool RemoveInside(EditSession session, List<Vector2> poly)
    {
        var inside = ThingsInside(poly, chosenKindsOnly: true);
        if (inside.Count == 0)
        {
            Say(Said.Of("area.msg.noObjects"));
            return false;
        }
        // A linked group's root takes its parts with it (ObjectLinks: a ship).
        var list = session.WithLinked(inside, anyPart: false).Indices;
        session.Commit(Lang.T("area.undo.removed", list.Count), null, list, []);
        Say(Said.Of("area.msg.removed", list.Count));
        return true;
    }

    /// <summary>Replaces the objects of the kind chosen in Replace inside the selection with the kind chosen in With.</summary>
    /// <param name="session">The session the replacement is committed to.</param>
    /// <param name="poly">The selection, in metres from the scene's corner.</param>
    /// <returns>False when a kind is not chosen on both lists (it says so).</returns>
    private bool ReplaceChosen(EditSession session, List<Vector2> poly)
    {
        var s = _view.Scene!;
        // By the list's own names: a name holding " (" (a mod's) cannot be cut back out of the entry.
        string? from = FromBox.SelectedIndex >= 0 && FromBox.SelectedIndex < _fromNames.Count ? _fromNames[FromBox.SelectedIndex] : null;
        int to = ToBox.SelectedIndex >= 0 && ToBox.SelectedIndex < _creatable.Count ? _creatable[ToBox.SelectedIndex] : 0;
        if (from == null || to == 0)
        {
            Say(Said.Of("area.msg.chooseReplace"));
            return false;
        }
        var list = ThingsInside(poly, chosenKindsOnly: false).Where(i => NameOf(s.Things[i].Prefab) == from).ToList();
        Replace(session, list, to);
        return true;
    }
}
