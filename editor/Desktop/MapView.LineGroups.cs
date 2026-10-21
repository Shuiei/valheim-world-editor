using Avalonia.OpenGL.Controls;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace TerrainEditor.Desktop;

/// <summary>
/// The groups of the map's lines, each with its colour: the grid, the edited zones, the resets and the chosen area.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// The view's frame for the map's lines: world metres to clip space, for the view's centre, size and zoom.
    /// </summary>
    /// <param name="Center">The world point at the middle of the view.</param>
    /// <param name="W">The view's width, in device pixels.</param>
    /// <param name="H">The view's height, in device pixels.</param>
    /// <param name="Mpp">Metres per device pixel.</param>
    internal readonly record struct LineFrame(Vector2 Center, int W, int H, float Mpp)
    {
        /// <summary>A world point in clip space.</summary>
        /// <param name="x">World X, in metres.</param>
        /// <param name="z">World Z, in metres.</param>
        /// <returns>The point, in clip space.</returns>
        public Vector2 C(float x, float z) => new((x - Center.X) / Mpp / (W / 2f), (z - Center.Y) / Mpp / (H / 2f));

        /// <summary>Adds the four sides of a box, as line segments.</summary>
        /// <param name="to">Where the segments go.</param>
        /// <param name="x0">The box's first world X, in metres.</param>
        /// <param name="z0">The box's first world Z, in metres.</param>
        /// <param name="x1">The box's last world X, in metres.</param>
        /// <param name="z1">The box's last world Z, in metres.</param>
        public void Box(List<float> to, float x0, float z0, float x1, float z1)
        {
            var a = C(x0, z0); var b = C(x1, z0); var c = C(x1, z1); var d = C(x0, z1);
            foreach (var (p, q) in new[] { (a, b), (b, c), (c, d), (d, a) })
            {
                to.AddRange([p.X, p.Y, 0, q.X, q.Y, 0]);
            }
        }
    }

    /// <summary>Adds the zone grid, when shown and zoomed in enough.</summary>
    /// <param name="groups">The line groups to add to.</param>
    /// <param name="f">The view's frame.</param>
    private void AddGrid(List<(float[] Data, Vector4 Color)> groups, LineFrame f)
    {
        float halfW = f.W / 2f * f.Mpp, halfH = f.H / 2f * f.Mpp;
        // Shown from 8 m a screen pixel, whatever the screen's scaling (mpp is per device pixel).
        if (ShowGrid && MetersPerPixel < 8)
        {
            var grid = new List<float>();
            for (float x = MathF.Floor((Center.X - halfW + 32) / 64) * 64 - 32; x <= Center.X + halfW; x += 64)
            {
                var a = f.C(x, Center.Y - halfH); var b = f.C(x, Center.Y + halfH);
                grid.AddRange([a.X, a.Y, 0, b.X, b.Y, 0]);
            }
            for (float z = MathF.Floor((Center.Y - halfH + 32) / 64) * 64 - 32; z <= Center.Y + halfH; z += 64)
            {
                var a = f.C(Center.X - halfW, z); var b = f.C(Center.X + halfW, z);
                grid.AddRange([a.X, a.Y, 0, b.X, b.Y, 0]);
            }
            groups.Add((grid.ToArray(), new Vector4(1, 1, 1, 0.25f)));
        }
    }

    /// <summary>Adds the edited zones: yellow, and orange where changed since the world was loaded.</summary>
    /// <param name="groups">The line groups to add to.</param>
    /// <param name="s">The world shown.</param>
    /// <param name="f">The view's frame.</param>
    private static void AddEdited(List<(float[] Data, Vector4 Color)> groups, WorldSession s, LineFrame f)
    {
        var edited = new List<float>();
        var changed = new List<float>();
        foreach (var (zx, zz, isChanged) in s.Edits.EditedZones())
        {
            f.Box(isChanged ? changed : edited, zx * 64 - 32, zz * 64 - 32, zx * 64 + 32, zz * 64 + 32);
        }
        groups.Add((edited.ToArray(), new Vector4(0.9f, 0.85f, 0.4f, 0.7f)));
        groups.Add((changed.ToArray(), new Vector4(1, 0.6f, 0.2f, 1)));
    }

    /// <summary>Adds the zones marked for reset, in red.</summary>
    /// <param name="groups">The line groups to add to.</param>
    /// <param name="s">The world shown.</param>
    /// <param name="f">The view's frame.</param>
    private static void AddResets(List<(float[] Data, Vector4 Color)> groups, WorldSession s, LineFrame f)
    {
        var resets = new List<float>();
        foreach (var r in s.Edits.Resets)
        {
            f.Box(resets, r.X * 64 - 32, r.Z * 64 - 32, r.X * 64 + 32, r.Z * 64 + 32);
        }
        groups.Add((resets.ToArray(), new Vector4(1, 0.31f, 0.25f, 1)));
    }

    /// <summary>Adds the chosen area, in blue.</summary>
    /// <param name="groups">The line groups to add to.</param>
    /// <param name="f">The view's frame.</param>
    private void AddArea(List<(float[] Data, Vector4 Color)> groups, LineFrame f)
    {
        if (Chosen is var (cx, cz, size))
        {
            var area = new List<float>();
            int x0 = cx - size / 2, z0 = cz - size / 2;
            f.Box(area, x0 * 64 - 32, z0 * 64 - 32, (x0 + size) * 64 - 32, (z0 + size) * 64 - 32);
            groups.Add((area.ToArray(), new Vector4(0.37f, 0.83f, 1, 1)));
        }
    }
}
