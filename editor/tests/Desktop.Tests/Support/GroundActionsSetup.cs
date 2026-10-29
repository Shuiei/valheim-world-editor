
namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Ground made up for the area and path tests: a flat area with a ripple, its height at a grid point and how uneven a rectangle is.
/// </summary>
internal static class GroundActionsSetup
{
    /// <summary>
    /// The height at grid point (x, z) of the area.
    /// </summary>
    internal static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

    /// <summary>
    /// Bumpy ground: a 1 m ripple raised over the whole area.
    /// </summary>
    internal static EditSession Bumpy()
    {
        var s = TestScenes.Flat(2);
        s.Shape(64, 64, Formula.Compile("sin(x) + cos(z)", ["x", "z", "d", "r", "h"]), 60, 0, "ripple");
        return s;
    }

    /// <summary>
    /// How uneven the ground is over a grid rectangle (corners included): the standard deviation of its heights, in
    /// metres.
    /// </summary>
    internal static double Spread(EditSession s, int x0, int z0, int x1, int z1)
    {
        var v = new List<double>();
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                v.Add(H(s, x, z));
            }
        }
        double m = v.Average();
        return Math.Sqrt(v.Average(h => (h - m) * (h - m)));
    }
}
