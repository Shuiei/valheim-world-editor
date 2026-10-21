using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    /// <summary>
    /// A cave along the line, worked out before the ground changes: for each point of the dense line, how far along it
    /// is, how deep the cave is (CaveDepth in the middle, an entrance slope at each end) and its floor.
    /// </summary>
    /// <param name="Curve">The dense line, as <see cref="PathTool.Curve"/> gives it.</param>
    /// <param name="Along">How far along the line each point is, in metres.</param>
    /// <param name="Depth">How deep the cave is below the ground's lie there, in metres.</param>
    /// <param name="Floor">The floor's height: the ground averaged over 6 m each way along the line (so bumps do not
    /// show in the floor), less the depth.</param>
    public sealed record CavePlan(List<(Vector2 P, int Seg)> Curve, float[] Along, float[] Depth, float[] Floor);

    /// <summary>Works out the cave along the current line from the ground as it is now.</summary>
    public CavePlan PlanCave(Ground g)
    {
        var curve = Curve();
        int n = curve.Count;
        float[] along = new float[n];
        for (int i = 1; i < n; i++)
        {
            along[i] = along[i - 1] + Vector2.Distance(curve[i].P, curve[i - 1].P);
        }
        float[] surface = [.. curve.Select(c => g.HeightOf(Math.Clamp((int)MathF.Round(c.P.Y), 0, g.H - 1) * g.W + Math.Clamp((int)MathF.Round(c.P.X), 0, g.W - 1)))];
        float total = n > 0 ? along[^1] : 0, entrance = MathF.Max(8, CaveDepth * 2.5f);
        float[] depth = new float[n];
        float[] floor = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0;
            int k = 0;
            for (int j = 0; j < n; j++)
            {
                if (MathF.Abs(along[j] - along[i]) <= 6)
                {
                    sum += surface[j];
                    k++;
                }
            }
            float t = Math.Clamp(MathF.Min(along[i], total - along[i]) / entrance, 0, 1);
            depth[i] = CaveDepth * t * t * (3 - 2 * t);
            floor[i] = sum / k - depth[i];
        }
        return new CavePlan(curve, along, depth, floor);
    }
}
