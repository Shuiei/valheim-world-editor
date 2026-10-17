using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Stability
{
    /// <summary>
    /// For each piece, the other pieces' colliders its grown boxes reach, by piece then collider order. Pieces are
    /// sorted into a grid by the sphere round their boxes, so only pieces whose spheres meet get the box test.
    /// </summary>
    private static List<(int J, Obb Collider)>[] Touches(Data?[] data, Obb[][] grown, Obb[][] seen)
    {
        int n = data.Length;
        var spheres = new (Vector3 C, float R)[n];
        var grid = new Dictionary<(int, int, int), List<int>>();
        var everywhere = new List<int>();
        for (int j = 0; j < n; j++)
        {
            if (data[j] is not { Supports: true } || seen[j].Length == 0)
            {
                continue;
            }
            spheres[j] = Sphere(seen[j]);
            if (!InCells(spheres[j], cell => (grid.TryGetValue(cell, out var l) ? l : grid[cell] = []).Add(j)))
            {
                everywhere.Add(j);
            }
        }
        var touches = new List<(int J, Obb Collider)>[n];
        var near = new List<int>();
        int[] marked = new int[n];
        for (int i = 0; i < n; i++)
        {
            touches[i] = [];
            if (data[i] == null || grown[i].Length == 0)
            {
                continue;
            }
            var sphere = Sphere(grown[i]);
            near.Clear();
            void Near(int j)
            {
                if (j != i && marked[j] != i + 1)
                {
                    marked[j] = i + 1;
                    near.Add(j);
                }
            }
            everywhere.ForEach(Near);
            if (!InCells(sphere, cell => grid.GetValueOrDefault(cell)?.ForEach(Near)))
            {
                foreach (var l in grid.Values)
                {
                    l.ForEach(Near);
                }
            }
            near.Sort();
            foreach (int j in near)
            {
                if (Vector3.Distance(sphere.C, spheres[j].C) > sphere.R + spheres[j].R)
                {
                    continue;
                }
                foreach (var col in seen[j])
                {
                    foreach (var g in grown[i])
                    {
                        if (Overlap(g, col))
                        {
                            touches[i].Add((j, col));
                            break;
                        }
                    }
                }
            }
        }
        return touches;
    }

    /// <summary>
    /// A sphere holding every box's own sphere (<see cref="Obb.Radius"/> round its middle), a little larger for
    /// rounding: boxes of two pieces whose spheres do not meet cannot overlap.
    /// </summary>
    private static (Vector3 C, float R) Sphere(Obb[] boxes)
    {
        var c = Vector3.Zero;
        foreach (var b in boxes)
        {
            c += b.C;
        }
        c /= boxes.Length;
        float r = 0;
        foreach (var b in boxes)
        {
            r = MathF.Max(r, Vector3.Distance(c, b.C) + b.Radius);
        }
        return (c, r * 1.001f + 0.01f);
    }

    /// <summary>Calls <paramref name="each"/> for every grid cell a sphere's bounds reach; false (none called) when they reach too many.</summary>
    private static bool InCells((Vector3 C, float R) s, Action<(int, int, int)> each)
    {
        static Vector3 Cell(Vector3 p) => new(MathF.Floor(p.X / TouchCell), MathF.Floor(p.Y / TouchCell), MathF.Floor(p.Z / TouchCell));
        var lo = Cell(s.C - new Vector3(s.R));
        var hi = Cell(s.C + new Vector3(s.R));
        var span = hi - lo + Vector3.One;
        if (!(span.X * span.Y * span.Z <= MaxTouchCells) || Vector3.Abs(lo) is not { X: < 1e8f, Y: < 1e8f, Z: < 1e8f } || Vector3.Abs(hi) is not { X: < 1e8f, Y: < 1e8f, Z: < 1e8f })
        {
            return false;
        }
        for (int x = (int)lo.X; x <= (int)hi.X; x++)
        {
            for (int y = (int)lo.Y; y <= (int)hi.Y; y++)
            {
                for (int z = (int)lo.Z; z <= (int)hi.Z; z++)
                {
                    each((x, y, z));
                }
            }
        }
        return true;
    }
}
