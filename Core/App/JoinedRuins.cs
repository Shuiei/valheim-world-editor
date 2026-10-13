using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// The pieces of a ruin a player built onto (a stone tower patched with wood): building pieces without a builder
/// (generated with the world) that touch a player-built piece, or touch one that does, and so on. They are shown as
/// part of the player's building, not hidden with the other ruins (spoilers).
/// </summary>
public static class JoinedRuins
{
    /// <summary>
    /// How near two pieces' origins must be to touch, in metres: a 4 m stone wall's origin is 4 m from its
    /// neighbour's.
    /// </summary>
    public const float Reach = 4.5f;

    /// <summary>Which ruin pieces are joined to the built ones.</summary>
    /// <param name="built">Where the player-built pieces are.</param>
    /// <param name="ruins">Where the ruins' pieces are.</param>
    /// <returns>For each ruin piece, whether it is joined.</returns>
    public static bool[] Find(IReadOnlyList<Vector3> built, IReadOnlyList<Vector3> ruins)
    {
        bool[] joined = new bool[ruins.Count];
        if (built.Count == 0 || ruins.Count == 0)
        {
            return joined;
        }
        // The ruin pieces by cube of Reach: the ones touching a point are in its cube and the 26 around it.
        static (int, int, int) Cell(Vector3 p) => ((int)MathF.Floor(p.X / Reach), (int)MathF.Floor(p.Y / Reach), (int)MathF.Floor(p.Z / Reach));
        var cells = new Dictionary<(int, int, int), List<int>>();
        for (int i = 0; i < ruins.Count; i++)
        {
            var c = Cell(ruins[i]);
            if (!cells.TryGetValue(c, out var list))
            {
                cells[c] = list = [];
            }
            list.Add(i);
        }
        var queue = new Queue<int>();
        void Touching(Vector3 p)
        {
            var (cx, cy, cz) = Cell(p);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!cells.TryGetValue((cx + dx, cy + dy, cz + dz), out var list))
                        {
                            continue;
                        }
                        foreach (int i in list)
                        {
                            if (!joined[i] && Vector3.DistanceSquared(p, ruins[i]) <= Reach * Reach)
                            {
                                joined[i] = true;
                                queue.Enqueue(i);
                            }
                        }
                    }
                }
            }
        }
        foreach (var p in built)
        {
            Touching(p);
        }
        while (queue.Count > 0)
        {
            Touching(ruins[queue.Dequeue()]);
        }
        return joined;
    }
}
