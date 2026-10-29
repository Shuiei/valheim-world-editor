
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// The boxes of every drawn object but the selected ones, for a drop: only those with a top to land on (see
    /// <see cref="HasTop"/>).
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="sel">The selected things' indices.</param>
    private DropBoxes DropIndex(WorldScene s, HashSet<int> sel)
    {
        var boxes = new DropBoxes();
        for (int i = 0; i < s.Things.Count; i++)
        {
            if (sel.Contains(i) || !_view.IsPickable(i) || !HasTop(_view.KindOf(i)))
            {
                continue;
            }
            var (bl, bh) = _view.BoundsOf(i);
            boxes.Add(bl, bh);
        }
        return boxes;
    }

    /// <summary>
    /// Whether a moved object may land on things of this kind: a floor, a table, a rock, a ruin... but not a tree, a bush,
    /// a pickable, a creature, a spawn point or an effect, whose box's top (a canopy, a mist) holds nothing up: a tree
    /// moved in a wood landed on the other trees' tops.
    /// </summary>
    /// <param name="kind">The thing's View kind.</param>
    internal static bool HasTop(ObjectKind kind) => kind is not (ObjectKind.Trees or ObjectKind.Bushes or ObjectKind.Pickables
        or ObjectKind.Animals or ObjectKind.Creatures or ObjectKind.SpawnPoints or ObjectKind.Effects);

    /// <summary>
    /// The selection's middle (height: its lowest object), the first one's turn (-180..180) and how many there
    /// are, of the objects a move takes (not runestones, unless only runestones are selected); null when nothing is
    /// selected.
    /// </summary>
    public (float X, float Y, float Z, float Turn, int Count)? Where()
    {
        var s = Scene;
        if (s == null)
        {
            return null;
        }
        // With the rest of the linked groups (a ship), as a move takes them: the middle is the whole group's.
        var chosen = Session?.WithLinked(_view.Selected).Indices ?? [.. _view.Selected];
        var things = chosen.Where(i => i >= 0 && i < s.Things.Count).Select(i => s.Things[i]).Where(t => !t.Gone).ToList();
        if (things.Count == 0)
        {
            return null;
        }
        // Runestones do not move (Begin leaves them out): the place is the moved objects', unless there are only
        // runestones.
        if (things.Where(t => !t.Runestone).ToList() is { Count: > 0 } moved)
        {
            things = moved;
        }
        float turn = ((things[0].Rotation.Y + 180) % 360 + 360) % 360 - 180;
        return (things.Average(t => t.Position.X), things.Min(t => t.Position.Y), things.Average(t => t.Position.Z), turn, things.Count);
    }
}
