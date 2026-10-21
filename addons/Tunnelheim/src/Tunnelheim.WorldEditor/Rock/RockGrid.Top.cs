namespace Tunnelheim.WorldEditor;

/// <summary>The highest rock surface over a point of the grid (RockGrid.Top.cs).</summary>
public sealed partial class RockGrid
{
    /// <summary>
    /// The height of the topmost rock surface at a grid point (VoxelWorld.TopRockAt): from 6 m above the ground the
    /// column was dug from, up through any rock built higher, then down to the first rock with air above it, between its
    /// samples; the lowest the rock may be opened to when there is none.
    /// </summary>
    /// <param name="gx">The point's grid x.</param>
    /// <param name="gz">Its grid z.</param>
    public float TopRockAt(int gx, int gz)
    {
        float seed = SeedAt(gx, gz);
        int bottom = RockMath.FloorStep((float)(seed - MaxDepth - 2.0));
        int gy = RockMath.CeilStep((float)(seed + 6.0));
        for (int climbed = 0; climbed < 256 && DensityAt(gx, gy, gz) > 0f; climbed++)
        {
            gy++;
        }
        float above = DensityAt(gx, gy + 1, gz);
        for (int y = gy; y > bottom; y--)
        {
            float d = DensityAt(gx, y, gz);
            if (d > 0f)
            {
                return (float)((y + (above <= 0f ? d / ((double)d - above) : 0.0)) * Step);
            }
            above = d;
        }
        return bottom * Step;
    }
}
