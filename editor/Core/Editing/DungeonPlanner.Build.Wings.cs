using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Places the level's side rooms (the rooms off the way): wings, some with a twin across, some leading further.
    /// </summary>
    /// <param name="l">The level.</param>
    /// <param name="sides">How many side rooms the level's size asks for.</param>
    private void PlaceWings(DungeonPlan.Level l, int sides)
    {
        DungeonPlan.Space? wing = null;
        for (int i = 0, tries = 0; i < sides && tries < sides * 6; tries++)
        {
            // Wings: a side room often grows off the last one, and what it is leans to what that one is (a kitchen
            // by the feast hall, cells by the guard room, an ossuary by the burial chamber: rooms that belong together).
            var parent = wing != null && CanParent(wing) && _rnd.NextDouble() < 0.55 ? wing : SideParent(l);
            if (parent == null)
            {
                break;
            }
            var n = AddWing(l, parent, out int placed);
            if (n == null)
            {
                continue;
            }
            wing = n;
            i += placed;
        }
    }

    /// <summary>
    /// Adds one side room off <paramref name="parent"/>, of a kind that leans to the parent's kind, and maybe its twin
    /// across the parent.
    /// </summary>
    /// <param name="l">The level.</param>
    /// <param name="parent">The space the side room opens off.</param>
    /// <param name="placed">How many rooms were added: 0 (none), 1, or 2 with the twin.</param>
    /// <returns>The side room, or null when its kind is already used or it does not fit.</returns>
    private DungeonPlan.Space? AddWing(DungeonPlan.Level l, DungeonPlan.Space parent, out int placed)
    {
        placed = 0;
        var room = Pick([.. _style.Side.Select(s => (s.Room, s.Weight * (Kin(parent.Room).Contains(s.Room) ? 5f : 1f)))]);
        if (SpecOf(room).Unique && _used.Contains(room))
        {
            return null;
        }
        var n = Attach(l, parent, room, main: false);
        if (n == null)
        {
            return null;
        }
        placed = 1;
        if (_rnd.NextDouble() < _style.Symmetry && Twin(l, n) != null)
        {
            placed = 2;
        }
        return n;
    }
}
