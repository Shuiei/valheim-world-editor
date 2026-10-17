
namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// One row of zones more on one side of a run of zones (first zone, count), up to <c>MaxFollowZones</c>.
    /// True when it grew.
    /// </summary>
    internal static bool Grow(ref int first, ref int count, bool up)
    {
        if (count >= MaxFollowZones)
        {
            return false;
        }
        first -= up ? 0 : 1;
        count++;
        return true;
    }

    /// <summary>
    /// The block Follow grows an area (south-west zone and size) to: a row of zones more each way the view is near an
    /// edge, both near a corner, each up to <c>MaxFollowZones</c>. Null when neither way can grow.
    /// </summary>
    /// <param name="block">The area's block.</param>
    /// <param name="upX">Near the east edge (true) or the west (false); null: near neither.</param>
    /// <param name="upZ">Near the north edge (true) or the south (false); null: near neither.</param>
    internal static (int X0, int Z0, int ZonesX, int ZonesZ)? Grown((int X0, int Z0, int ZonesX, int ZonesZ) block, bool? upX, bool? upZ)
    {
        bool grownX = upX is { } east && Grow(ref block.X0, ref block.ZonesX, east);
        bool grownZ = upZ is { } north && Grow(ref block.Z0, ref block.ZonesZ, north);
        return grownX || grownZ ? block : null;
    }
}
