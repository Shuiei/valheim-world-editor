using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// Elevation: picking the height an object stands at, and nudging it.
public sealed partial class PlaceTool
{
    /// <summary>
    /// Alt + click: one height for all, from the top of the piece clicked (to build on it) or the ground.
    /// </summary>
    /// <param name="thing">The thing clicked, if any.</param>
    /// <param name="groundY">The height clicked on the ground.</param>
    /// <returns>The line for the status bar.</returns>
    public Said PickElevation(int? thing, float groundY)
    {
        var s = Scene();
        float y = groundY;
        string? top = null;
        if (s != null && thing is int i && PieceCatalog.Get(s.Things[i].Prefab) is { Snaps.Length: > 0 } info)
        {
            var t = s.Things[i];
            y = info.Snaps.Max(p => RotU(new Vector3(p[0], p[1], p[2]), t.Rotation).Y) + t.Position.Y;
            top = NameOf(t.Prefab) ?? info.Name;
        }
        Elevation = Elevations.At;
        Elev = MathF.Round(y, 2);
        Notify();
        return top == null ? Said.Of("place.msg.elevationGround", Elev) : Said.Of("place.msg.elevationTop", Elev, top);
    }

    /// <summary>
    /// PgUp / PgDn: the elevation up or down by <c>d</c> metres; from the ground they start lifting above it. Returns
    /// the line for the status bar.
    /// </summary>
    public Said NudgeElevation(float d)
    {
        if (Elevation == Elevations.Ground)
        {
            Elevation = Elevations.Above;
            Elev = 0;
        }
        Elev = MathF.Round(Elev + d, 2);
        Notify();
        return Elevation == Elevations.At ? Said.Of("place.msg.elevationAt", Elev) : Said.Of("place.msg.elevationAbove", Elev);
    }
}
