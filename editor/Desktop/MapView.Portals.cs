using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using TerrainEditor.App;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace TerrainEditor.Desktop;

/// <summary>
/// The world's portals over the map: violet discs, hollow when alone, and a line between the two of each pair.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>What the portals' shapes were last made for: the portals and the zoom.</summary>
    private (PortalMap.Result? Portals, float Mpp) _portalsKey;

    /// <summary>The portals' shapes last made (<see cref="PortalShapes"/>).</summary>
    private float[] _portalShapes = [];

    /// <summary>
    /// The portals (violet discs, hollow when alone) and the links between the paired ones (violet lines), when
    /// <c>ShowPortals</c> is on.
    /// </summary>
    private void DrawPortals()
    {
        if (ShowPortals && Portals is { } portals)
        {
            Fill(PortalShapes(portals), PrimitiveType.Triangles);
        }
    }

    /// <summary>
    /// The shapes of <c>DrawPortals</c>, as triangles (x, z then RGBA per corner): made again only when the portals or
    /// the zoom changed, else the last ones as they are.
    /// </summary>
    /// <param name="portals">The portals read.</param>
    internal float[] PortalShapes(PortalMap.Result portals)
    {
        float m = MetersPerPixel;
        var key = (portals, m);
        if (_portalsKey == key)
        {
            return _portalShapes;
        }
        _portalsKey = key;
        var data = new List<float>();
        var violet = new Vector4(190 / 255f, 120 / 255f, 1, 1);
        var dark = new Vector4(40 / 255f, 15 / 255f, 70 / 255f, 0.9f);
        foreach (var (a, b) in portals.Links)
        {
            Vector2 p = new(portals.Portals[a].X, portals.Portals[a].Z), q = new(portals.Portals[b].X, portals.Portals[b].Z);
            Vector2 d = q - p;
            if (d.LengthSquared() < 1e-6f)
            {
                continue;
            }
            // A line 2 pixels wide, as a quad: the direction turned a right angle, half the width long.
            Vector2 n = Vector2.Normalize(new(-d.Y, d.X)) * m;
            Quad(data, p + n, q + n, q - n, p - n, new Vector4(violet.X, violet.Y, violet.Z, 0.85f));
        }
        for (int i = 0; i < portals.Portals.Count; i++)
        {
            var o = portals.Portals[i];
            Disc(data, o.X, o.Z, 7 * m, dark, 12);
            Disc(data, o.X, o.Z, 5.5f * m, violet, 12);
            if (!portals.IsLinked(i))
            {
                // Alone: a hollow disc.
                Disc(data, o.X, o.Z, 3 * m, dark, 12);
            }
        }
        _portalShapes = [.. data];
        return _portalShapes;
    }
}
