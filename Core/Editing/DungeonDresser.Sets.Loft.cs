using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- The gallery.
    /// <summary>
    /// A loft over the way in of a tall stone room: a deck 2 m deep along the near wall, 4 m up on posts, a rail
    /// along its edge, stairs up one side (8 m of them: a stair rises 1 m over 2), braziers and a sentinel on it.
    /// None when the room is low, not of stone, too small for the stairs, has a tall doorway (a grand gate, a
    /// curtain) in that wall or its sides are not free along the stairs.
    /// </summary>
    /// <returns>Whether the loft stands (its stairs then keep their side: colonnades must check the floor is free).</returns>
    private bool Loft(Frame f)
    {
        if (!f.S.Tall || !Decked || f.HalfA < 3 || f.HalfD < 6 || f.S.Shape != DungeonPlan.Shape.Rect)
        {
            return false;
        }
        foreach (var m in f.Mouths)
        {
            var (_, mv) = f.Local(m.X, m.Z);
            if (mv < -f.HalfD + 1.5f && m.Door.Kind is Door.Grand or Door.Curtain)
            {
                return false;
            }
        }
        // The deck's row is the room's first; its edge 2 m in from the near wall's tiles.
        float edge = -f.Deep + 2;
        int side = 0;
        foreach (int s in new[] { -1, 1 }.OrderBy(_ => _rnd.Next()))
        {
            float tryU = s * (f.HalfA - 1.05f);
            if (Enumerable.Range(0, 4).All(i => f.Free(tryU, edge + 1 + 2 * i, 1.0f)))
            {
                side = s;
                break;
            }
        }
        if (side == 0)
        {
            return false;
        }
        // The deck, and the posts under its edge (none in front of a doorway).
        for (int a = 0; a < f.Across; a++)
        {
            float u = 2 * a + 1 - f.Across;
            _o.Add(Deck, f.W(u, edge - 1, f.F + 3.5f), f.Yaw(0));
            if (f.Free(u, edge - 0.5f, 0.6f))
            {
                Post(f, u, edge - 0.5f, 3);
            }
            if (a != (side < 0 ? 0 : f.Across - 1))
            {
                _o.Add("stone_fence", f.W(u, edge - 0.5f, f.F + 4 + Lift), f.Yaw(0));
            }
        }
        // The stairs: a stepped base of slabs along the side wall, a stair on each step, one on the floor.
        float su = side * (f.HalfA - 1.05f);
        for (int k = 0; k < 4; k++)
        {
            float v = edge + 1 + 2 * k;
            for (int b = 0; b < 3 - k; b++)
            {
                _o.Add(Deck, f.W(su, v, f.F + 0.5f + b), f.Yaw(0));
            }
            _o.Add("stone_stair", f.W(su, v, f.F + (3 - k) + Lift), f.Yaw(0));
            f.Take(su, v, 1.2f);
        }
        // Up on the deck: braziers at both ends, a sentinel in the middle facing the room, skulls.
        float top = f.F + 4 + Lift;
        float deckV = edge - 1;
        _o.Add(_b.Brazier, f.W(-(f.HalfA - 1.1f), deckV, top + DungeonProps.Foot(_b.Brazier)), f.Yaw(0));
        _o.Add(_b.Brazier, f.W(f.HalfA - 1.1f, deckV, top + DungeonProps.Foot(_b.Brazier)), f.Yaw(0));
        string sentinel = _style.Name switch
        {
            "Crypt" or "Catacombs" or "Ruins" => One("StatueEvil", "StatueDeer", "skull_pile"),
            "Temple" => One("StatueFreya", "StatueThor"),
            "Goblin warren" => "goblin_totempole",
            "Dvergr hold" => "dvergrprops_lantern_standing",
            _ => One("ArmorStand_Male", "Morkhalla_WeaponStand"),
        };
        _o.Add(sentinel, f.W(0, deckV - 0.2f, top + DungeonProps.Foot(sentinel)), f.Yaw(0));
        return true;
    }
}
