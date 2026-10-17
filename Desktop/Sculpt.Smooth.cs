
namespace TerrainEditor.Desktop;

// Smooth: each point is pulled towards the average of its nine neighbours, read before the frame.
public static partial class Sculpt
{
    /// <summary>The heights of the brush rectangle and a point around it, as they are before the frame.</summary>
    private static float[] TakeSnapshot(Ground g, int x0, int x1, int z0, int z1)
    {
        int sw = x1 - x0 + 3;
        float[] snapshot = new float[sw * (z1 - z0 + 3)];
        for (int gz = z0 - 1; gz <= z1 + 1; gz++)
        {
            for (int gx = x0 - 1; gx <= x1 + 1; gx++)
            {
                snapshot[(gz - z0 + 1) * sw + gx - x0 + 1] = g.HeightOf(gz * g.W + gx);
            }
        }
        return snapshot;
    }

    /// <summary>The average of the nine heights around a point, from the snapshot taken before the frame.</summary>
    private static float SmoothAverage(Frame f, int gx, int gz)
    {
        float sum = 0;
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                sum += f.Snapshot![(gz + dz - f.Z0 + 1) * f.Sw + gx + dx - f.X0 + 1];
            }
        }
        return sum / 9;
    }
}
