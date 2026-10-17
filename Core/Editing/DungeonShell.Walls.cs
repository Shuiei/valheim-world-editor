using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal static partial class DungeonShell
{
    /// <summary>A wall's piece of plane: along the X axis (a plane x = Line) or the Z axis (z = Line), metres.</summary>
    /// <param name="AlongZ">The wall runs along z, in the plane x = Line; else along x, in z = Line.</param>
    /// <param name="Line">Where the plane is (metres).</param>
    /// <param name="Index">The tile it borders, counted along the wall.</param>
    /// <param name="Y0">Its foot (metres).</param>
    /// <param name="Y1">Its top (metres).</param>
    private sealed record Edge(bool AlongZ, float Line, int Index, float Y0, float Y1);

    /// <summary>
    /// Walls between tiles of different spaces and round the outside, except in doorways and between
    /// corridors; each run filled with the material's pieces, posts at its ends if the material has them.
    /// </summary>
    private static void Walls(DungeonPlan plan, DungeonPlan.Level l, float f, Material mat, DungeonOut o)
    {
        var posts = new HashSet<(float, float, float, float)>();
        foreach (var g in WallEdges(plan, l, f).GroupBy(e => (e.AlongZ, e.Line, e.Y0, e.Y1)))
        {
            var idx = g.Select(e => e.Index).Distinct().OrderBy(i => i).ToList();
            for (int a = 0; a < idx.Count;)
            {
                int b = a;
                while (b + 1 < idx.Count && idx[b + 1] == idx[b] + 1)
                {
                    b++;
                }
                float t0 = 2 * idx[a], t1 = 2 * idx[b] + 2;
                Fill(mat, o, g.Key.AlongZ, g.Key.Line, t0, t1, g.Key.Y0, g.Key.Y1);
                if (mat.Post != null)
                {
                    posts.Add((g.Key.AlongZ ? g.Key.Line : t0, g.Key.AlongZ ? t0 : g.Key.Line, g.Key.Y0, g.Key.Y1));
                    posts.Add((g.Key.AlongZ ? g.Key.Line : t1, g.Key.AlongZ ? t1 : g.Key.Line, g.Key.Y0, g.Key.Y1));
                }
                a = b + 1;
            }
        }
        foreach (var (x, z, y0, y1) in posts)
        {
            Stack(mat.Post!, o, x, z, y0, y1);
        }
    }
}
