using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Stability
{
    /// <summary>Works out every piece's support and what falls; pieces the scan does not know are left out (Free).</summary>
    /// <param name="pieces">The pieces.</param>
    /// <param name="groundAt">The ground's height at a world x, z.</param>
    public static Result Solve(IReadOnlyList<Piece> pieces, Func<float, float, float> groundAt)
    {
        int n = pieces.Count;
        var catalogue = _pieces;
        var data = pieces.Select(p => catalogue.GetValueOrDefault(p.Prefab)).ToArray();
        // Each piece's grown boxes (what it looks with) and its colliders that others see.
        var grown = new Obb[n][];
        var seen = new Obb[n][];
        var com = new Vector3[n];
        bool[] grounded = new bool[n];
        for (int i = 0; i < n; i++)
        {
            var d = data[i];
            if (d == null)
            {
                grown[i] = seen[i] = [];
                continue;
            }
            grown[i] = [.. d.Boxes.Select(b => World(b, pieces[i], 0.15f))];
            seen[i] = [.. d.Boxes.Where(b => b.Counts).Select(b => World(b, pieces[i], 0f))];
            com[i] = Com(d, pieces[i]);
            // Touching the terrain: a grown box reaching the ground anywhere under it.
            grounded[i] = d.Free || grown[i].Any(g => g.Corners().Append(g.C).Any(c => c.Y <= groundAt(c.X, c.Z)));
        }
        // Who touches whom (the boxes do not move): for each piece, the other pieces' colliders its own
        // grown boxes reach.
        var touches = Touches(data, grown, seen);
        float[] support = new float[n];
        bool[] alive = new bool[n];
        for (int i = 0; i < n; i++)
        {
            alive[i] = data[i] != null;
            support[i] = data[i] != null ? Material(data[i]!.Material).Max : 0;
        }
        var falls = new List<int>();
        while (true)
        {
            // Settle (from full support down, as new pieces start in game).
            support = Settle(support, alive, data, com, grounded, touches, pieces);
            // What breaks first: the pieces below their minimum; then the rest again without them.
            var broken = Enumerable.Range(0, n).Where(i => alive[i] && support[i] < Material(data[i]!.Material).Min).ToList();
            if (broken.Count == 0)
            {
                break;
            }
            foreach (int i in broken)
            {
                alive[i] = false;
                falls.Add(i);
            }
        }
        return Verdict(support, data, falls);
    }

    /// <summary>Turns the settled support into each piece's colour, break and free flags.</summary>
    /// <param name="support">The settled support per piece.</param>
    /// <param name="data">Each piece's data, or null when unknown.</param>
    /// <param name="falls">The pieces that broke, in the order they fell.</param>
    internal static Result Verdict(float[] support, Data?[] data, List<int> falls)
    {
        int n = support.Length;
        float[] colour = new float[n];
        bool[] breaks = new bool[n];
        bool[] free = new bool[n];
        foreach (int i in falls)
        {
            breaks[i] = true;
        }
        for (int i = 0; i < n; i++)
        {
            free[i] = data[i] == null;
            if (data[i] == null)
            {
                continue;
            }
            var (max, min, _, _) = Material(data[i]!.Material);
            // WearNTear.GetSupportColorValue.
            colour[i] = breaks[i] ? 0 : support[i] >= max ? -1 : Math.Clamp((support[i] - min) / (max * 0.5f - min), 0, 1);
        }
        return new Result(support, colour, breaks, free, falls);
    }
}
