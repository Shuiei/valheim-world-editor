using System.Numerics;

namespace TerrainEditor.Desktop;

// Snapping one piece to the pieces there: beside one, or on top of the one under the cursor.
public sealed partial class PlaceTool
{
    /// <summary>A piece's facing: the reference facing plus the rotation rounded to quarter turns.</summary>
    private float AlignYaw(float reference) => reference + 90 * MathF.Round(Rotation / 90);

    /// <summary>
    /// Snaps one piece to the pieces within 12 m: on top of the one under the cursor (or the nearest within 3 m), or
    /// beside the nearest where a pair of snap points meets within 1 m. Returns it unchanged when nothing snaps.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="o">The piece where the cursor puts it.</param>
    /// <param name="others">The pieces already there.</param>
    /// <param name="under">The thing under the cursor, for On top.</param>
    private Placement SnapSingle(WorldScene s, Placement o, List<Other> others, int? under)
    {
        Snapped = null;
        var sp = SnapsOf(o.Name).Select(p => new Vector3(p[0], p[1], p[2])).ToList();
        var near = others.Where(x => MathF.Abs(x.G.X - o.G.X) < 12 && MathF.Abs(x.G.Y - o.G.Y) < 12).ToList();
        if (near.Count == 0)
        {
            return o;
        }
        float ox = s.OriginX, oz = s.OriginZ;
        Placement Place(Vector2 g, float y, float ry) => o with { G = g, Position = new Vector3(ox + g.X, y, oz + g.Y), Rotation = new Vector3(0, (ry % 360 + 360) % 360, 0), Scale = 0 };
        if (OnTop)
        {
            var below = near.FirstOrDefault(x => x.Index == under)
                ?? near.Where(x => Vector2.Distance(x.G, o.G) < 3).OrderBy(x => Vector2.Distance(x.G, o.G)).FirstOrDefault();
            if (below != null)
            {
                float top = below.Pts.Max(p => p.Y);
                var tops = below.Pts.Where(p => p.Y > top - 0.05f).ToList();
                float ry = AlignYaw(below.Rotation.Y);
                var mine = sp.Select(p => RotU(p, new Vector3(0, ry, 0))).ToList();
                float low = mine.Min(p => p.Y);
                var bottoms = mine.Where(p => p.Y < low + 0.05f).ToList();
                (float D, Vector2 G)? best = null;
                foreach (var t in tops)
                {
                    foreach (var m in bottoms)
                    {
                        var g = new Vector2(t.X - m.X, t.Z - m.Z);
                        float d = Vector2.Distance(g, o.G);
                        if (best == null || d < best.Value.D)
                        {
                            best = (d, g);
                        }
                    }
                }
                Snapped = (SnapWays.OnTop, below.Name);
                return Place(best!.Value.G, top - low, ry);
            }
        }
        // Beside: turned like the nearest piece (the quarter turn that meets best, the facing asked for
        // first), then the closest pair of snap points along the ground (within 1 m) meets.
        var close = near.OrderBy(x => Vector2.Distance(x.G, o.G)).First();
        float[] yaws = Vector2.Distance(close.G, o.G) < 5 ? [.. new[] { 0f, 180, 90, 270 }.Select(k => AlignYaw(close.Rotation.Y) + k)] : [o.Rotation.Y];
        (float Score, Vector3 Q, Vector3 M, float Ry, string Name)? bestB = null;
        for (int k = 0; k < yaws.Length; k++)
        {
            var mine = sp.Select(p => RotU(p, new Vector3(0, yaws[k], 0)) + new Vector3(o.G.X, o.Position.Y, o.G.Y)).ToList();
            float myLow = mine.Min(p => p.Y);
            foreach (var x in near)
            {
                foreach (var q in x.Pts)
                {
                    foreach (var m in mine)
                    {
                        float d = MathF.Sqrt((q.X - m.X) * (q.X - m.X) + (q.Z - m.Z) * (q.Z - m.Z));
                        if (d > 1)
                        {
                            continue;
                        }
                        float score = d + MathF.Abs(q.Y - x.Low - (m.Y - myLow)) + 0.01f * k;
                        if (bestB == null || score < bestB.Value.Score)
                        {
                            bestB = (score, q, m, yaws[k], x.Name);
                        }
                    }
                }
            }
        }
        if (bestB is not { } bb)
        {
            return o;
        }
        Snapped = (SnapWays.Beside, bb.Name);
        return Place(o.G + new Vector2(bb.Q.X - bb.M.X, bb.Q.Z - bb.M.Z), o.Position.Y + bb.Q.Y - bb.M.Y, bb.Ry);
    }
}
