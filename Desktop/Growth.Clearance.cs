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
        // Cells wider than the farthest test, so a spot looks only at its own cell and the eight around it.
        var index = new CellNeighbourIndex<(float X, float Z, string? Name, ObjectKind Kind)>(Farthest + 1);
        foreach (var s in standing)
        {
            index.Add(s.X, s.Z, s);
        }
        bool Near(Regrow.Spot o, float d, Func<string?, ObjectKind, bool> test) =>
            index.AnyNear(o.X, o.Z, s => MathF.Abs(s.X - o.X) < d && MathF.Abs(s.Z - o.Z) < d && MathF.Sqrt((s.X - o.X) * (s.X - o.X) + (s.Z - o.Z) * (s.Z - o.Z)) < d && test(s.Name, s.Kind));
        return [.. kept.Where(o => !Near(o, 1, (_, _) => true) && !Near(o, 3, (n, _) => n == o.Name) && !Near(o, Farthest, (_, k) => k == ObjectKind.Buildings))];
    }
}
