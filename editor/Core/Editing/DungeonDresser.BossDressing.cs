
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>The boss room's walls: the statue and banners of a tall room, or the altar of a low one (none in the dais room).</summary>
    /// <param name="f">The room's frame.</param>
    /// <param name="variant">Which arena was laid out.</param>
    private void DressWalls(Frame f, int variant)
    {
        if (f.S.Tall)
        {
            if (variant is not (1 or 3))
            {
                Put(f, "StatueEvil", 0, f.HalfD - 1.6f, 180, 1.6f);
            }
            foreach (int side in new[] { -1, 1 })
            {
                _o.Add(_b.Name == "Ashlands" ? "CharredBanner1" : "cloth_hanging_long", f.W(side * 3.4f, f.HalfD - 0.15f, f.Top - 0.25f), f.Yaw(180));
            }
            _o.Add("Morkhalla_StatueSword_hanging", f.W(0, f.HalfD - 5, f.Top + f.H - 2.7f), f.Yaw(0));
        }
        else if (variant != 1)
        {
            Put(f, One("Ashlands_Altar", "offeraltar_FrozenKing_bossroom"), 0, f.HalfD - 1.6f, 180, 1.5f);
        }
    }

    /// <summary>The boss room's floor: the great rug, the giant's sword before the altar, the drapes and the skulls about the arena.</summary>
    /// <param name="f">The room's frame.</param>
    /// <param name="variant">Which arena was laid out.</param>
    /// <param name="r">The arena's radius (metres).</param>
    private void DressFloor(Frame f, int variant, float r)
    {
        // A great rug in the middle, the boss upon it; a giant's sword before the altar; banners.
        foreach (var (cu, cv, yaw) in new[] { (-2.18f, -2.18f, 0f), (2.18f, -2.18f, 90f), (2.18f, 2.18f, 180f), (-2.18f, 2.18f, 270f) })
        {
            _o.Add("Morkhalla_Rug_corner", f.W(cu, cv, f.Top + 0.06f), f.Yaw(yaw));
        }
        if (f.HalfA >= 10 && variant != 1)
        {
            _o.Add("Morkhalla_StatueSword", f.W(0, f.HalfD - 4.4f, f.Top + 0.3f), f.Yaw(R(-8, 8)));
            f.Take(-3, f.HalfD - 4.4f, 1.2f);
            f.Take(3, f.HalfD - 4.4f, 1.2f);
        }
        Hangings(f, 4, Drapes);
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.PI / 2;
            PutNear(f, "skull_pile", (r + 1.4f) * MathF.Cos(a), (r + 1.4f) * MathF.Sin(a), R(0, 360), 0.6f, 1f);
        }
    }
}
