using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Stability
{
    /// <summary>Repeats the support round until nothing changes (at most 400 rounds) and returns the settled support.</summary>
    /// <param name="support">The support to start from.</param>
    /// <param name="alive">Which pieces still stand.</param>
    /// <param name="data">Each piece's data, or null when unknown.</param>
    /// <param name="com">Each piece's centre of mass.</param>
    /// <param name="grounded">Which pieces touch the terrain.</param>
    /// <param name="touches">The colliders each piece's boxes reach.</param>
    /// <param name="pieces">The pieces.</param>
    private static float[] Settle(float[] support, bool[] alive, Data?[] data, Vector3[] com, bool[] grounded, List<(int J, Obb Collider)>[] touches, IReadOnlyList<Piece> pieces)
    {
        var points = new List<(Vector3 P, float V)>();
        for (int round = 0; round < 400; round++)
        {
            float change = 0;
            float[] next = (float[])support.Clone();
            for (int i = 0; i < support.Length; i++)
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
                float best = ContactSupport(i, hLoss, vLoss, support, alive, com, touches[i], pieces, points);
                next[i] = MathF.Min(best, max);
                change = MathF.Max(change, MathF.Abs(next[i] - support[i]));
            }
            support = next;
            if (change < 1e-3f)
            {
                break;
            }
        }
        return support;
    }

    /// <summary>The best support piece <paramref name="i"/> gets from the pieces it rests on, before its material's cap.</summary>
    /// <param name="i">The piece.</param>
    /// <param name="hLoss">The material's loss per metre, sideways.</param>
    /// <param name="vLoss">The material's loss per metre, from above.</param>
    /// <param name="support">The current support per piece.</param>
    /// <param name="alive">Which pieces still stand.</param>
    /// <param name="com">Each piece's centre of mass.</param>
    /// <param name="contacts">The colliders piece <paramref name="i"/> touches.</param>
    /// <param name="pieces">The pieces.</param>
    /// <param name="points">Scratch list of the contact points under the piece's centre (cleared here).</param>
    private static float ContactSupport(int i, float hLoss, float vLoss, float[] support, bool[] alive, Vector3[] com, List<(int J, Obb Collider)> contacts, IReadOnlyList<Piece> pieces, List<(Vector3 P, float V)> points)
    {
        float best = 0;
        points.Clear();
        foreach (var (j, col) in contacts)
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
        return HeldFromTwoSides(points, com[i], best);
    }

    /// <summary>Held from two sides (at least 100° apart seen from above): the average of the two, when it beats <paramref name="best"/>.</summary>
    /// <param name="points">The contact points with their support.</param>
    /// <param name="centre">The piece's centre of mass.</param>
    /// <param name="best">The best support so far.</param>
    internal static float HeldFromTwoSides(List<(Vector3 P, float V)> points, Vector3 centre, float best)
    {
        for (int a = 0; a < points.Count - 1; a++)
        {
            var va = points[a].P - centre;
            va.Y = 0;
            for (int b = a + 1; b < points.Count; b++)
            {
                float avg = (points[a].V + points[b].V) * 0.5f;
                if (avg <= best)
                {
                    continue;
                }
                var vb = points[b].P - centre;
                vb.Y = 0;
                if (va.LengthSquared() > 1e-10f && vb.LengthSquared() > 1e-10f
                    && MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(va), Vector3.Normalize(vb)), -1, 1)) * 180 / MathF.PI >= 100f)
                {
                    best = avg;
                }
            }
        }
        return best;
    }
}
