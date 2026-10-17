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
        var points = new List<(Vector3 P, float V)>();
        while (true)
        {
            // Settle (from full support down, as new pieces start in game).
            for (int round = 0; round < 400; round++)
            {
                float change = 0;
                float[] next = (float[])support.Clone();
                for (int i = 0; i < n; i++)
                {
                    if (!alive[i])
                    {
                        continue;
                    }
                    var (max, _, hLoss, vLoss) = Material(data[i]!.Material);
                    if (grounded[i])
                    {
                        next[i] = max;
                        continue;
                    }
                    float best = 0;
                    points.Clear();
                    foreach (var (j, col) in touches[i])
                    {
                        if (!alive[j])
                        {
                            continue;
                        }
                        float s2 = support[j];
                        float dist = Vector3.Distance(com[i], com[j]) + 0.1f, toPos = Vector3.Distance(com[i], pieces[j].Position) + 0.1f;
                        if (toPos < dist)
                        {
                            dist = toPos;
                        }
                        best = MathF.Max(best, s2 - hLoss * dist * s2);
                        var sp = col.Closest(com[i]);
                        if (sp.Y < com[i].Y + 0.05f)
                        {
                            var dir = sp - com[i];
                            if (dir.LengthSquared() > 1e-10f)
                            {
                                dir = Vector3.Normalize(dir);
                                if (dir.Y < 0)
                                {
                                    float t = MathF.Acos(1f - MathF.Abs(dir.Y)) / (MathF.PI / 2f);
                                    float loss = hLoss + (vLoss - hLoss) * Math.Clamp(t, 0, 1);
                                    best = MathF.Max(best, s2 - loss * dist * s2);
                                }
                            }
                            points.Add((sp, s2 - vLoss * dist * s2));
                        }
                    }
                    // Held from two sides (at least 100° apart seen from above): the average of the two.
                    for (int a = 0; a < points.Count - 1; a++)
                    {
                        var va = points[a].P - com[i];
                        va.Y = 0;
                        for (int b = a + 1; b < points.Count; b++)
                        {
                            float avg = (points[a].V + points[b].V) * 0.5f;
                            if (avg <= best)
                            {
                                continue;
                            }
                            var vb = points[b].P - com[i];
                            vb.Y = 0;
                            if (va.LengthSquared() > 1e-10f && vb.LengthSquared() > 1e-10f
                                && MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(va), Vector3.Normalize(vb)), -1, 1)) * 180 / MathF.PI >= 100f)
                            {
                                best = avg;
                            }
                        }
                    }
                    next[i] = MathF.Min(best, max);
                    change = MathF.Max(change, MathF.Abs(next[i] - support[i]));
                }
                support = next;
                if (change < 1e-3f)
                {
                    break;
                }
            }
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
