using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class Growth
{
    /// <summary>
    /// The kept spots with nothing too near them: a spot is refused when anything stands within 1 m of it, the same
    /// kind within 3 m, or a building within the farthest test's reach.
    /// </summary>
    /// <param name="kept">The spots kept so far.</param>
    /// <param name="standing">What stands near them (<see cref="StandingNear"/>).</param>
    /// <returns>The spots that stay, in their order.</returns>
    internal static List<Regrow.Spot> Unobstructed(List<Regrow.Spot> kept, List<(float X, float Z, string? Name, ObjectKind Kind)> standing)
    {
        // In cells wider than the farthest test, so a spot looks only at its own cell and the eight around it.
        var cells = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < standing.Count; i++)
        {
            var key = (Cell(standing[i].X), Cell(standing[i].Z));
            if (!cells.TryGetValue(key, out var list))
            {
                cells[key] = list = [];
            }
            list.Add(i);
        }
        bool Near(Regrow.Spot o, float d, Func<string?, ObjectKind, bool> test)
        {
            int cx = Cell(o.X), cz = Cell(o.Z);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (cells.TryGetValue((cx + dx, cz + dz), out var list) && list.Exists(i =>
                    {
                        var (x, z, name, kind) = standing[i];
                        return MathF.Abs(x - o.X) < d && MathF.Abs(z - o.Z) < d && MathF.Sqrt((x - o.X) * (x - o.X) + (z - o.Z) * (z - o.Z)) < d && test(name, kind);
                    }))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        return [.. kept.Where(o => !Near(o, 1, (_, _) => true) && !Near(o, 3, (n, _) => n == o.Name) && !Near(o, Farthest, (_, k) => k == ObjectKind.Buildings))];
    }
}
