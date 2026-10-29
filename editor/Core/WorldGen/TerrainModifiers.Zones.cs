
namespace TerrainEditor.Terrain;

public sealed partial class TerrainModifiers
{
    /// <summary>
    /// Sorts the modifiers into the order the game applies them (TerrainModifier.SortByModifiers): player-made last, then by
    /// sort order, creation time and distance from the middle.
    /// </summary>
    internal static void SortByModifiers(List<Modifier> all)
    {
        all.Sort((a, b) =>
        {
            if (a.Player != b.Player)
            {
                return a.Player.CompareTo(b.Player);
            }

            if (a.SortOrder != b.SortOrder)
            {
                return a.SortOrder.CompareTo(b.SortOrder);
            }

            if (a.CreationTime != b.CreationTime)
            {
                return a.CreationTime.CompareTo(b.CreationTime);
            }

            return a.Position.LengthSquared().CompareTo(b.Position.LengthSquared());
        });
    }

    /// <summary>
    /// Adds each modifier to the zones its radius reaches, in the order given.
    /// </summary>
    internal void IndexByZone(List<Modifier> all)
    {
        foreach (Modifier m in all)
        {
            // Heightmap.TerrainVSModifier: radius + 4 m against the zone square.
            float r = m.Radius + 4f;
            int zx0 = (int)Math.Floor((m.Position.X - r + 32f) / 64f), zx1 = (int)Math.Floor((m.Position.X + r + 32f) / 64f);
            int zz0 = (int)Math.Floor((m.Position.Z - r + 32f) / 64f), zz1 = (int)Math.Floor((m.Position.Z + r + 32f) / 64f);
            for (int zz = zz0; zz <= zz1; zz++)
            {
                for (int zx = zx0; zx <= zx1; zx++)
                {
                    if (Overlaps(m, zx, zz))
                    {
                        (_byZone.TryGetValue((zx, zz), out var list) ? list : _byZone[(zx, zz)] = []).Add(m);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Degrees to radians.
    /// </summary>
    internal static float Deg(float d) => d * (MathF.PI / 180f);

    /// <summary>
    /// Heightmap.TerrainVSModifier: whether the modifier's radius + 4 m reaches the zone's square.
    /// </summary>
    internal static bool Overlaps(Modifier m, int zx, int zz)
    {
        float num = m.Radius + 4f;
        float cx = zx * 64f, cz = zz * 64f, half = 32f;
        return !(m.Position.X + num < cx - half || m.Position.X - num > cx + half || m.Position.Z + num < cz - half || m.Position.Z - num > cz + half);
    }
}
