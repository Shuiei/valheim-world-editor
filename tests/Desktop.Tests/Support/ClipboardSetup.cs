using System.Numerics;
using Avalonia.Headless;
using Avalonia.Input;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The areas, copies and windows the copy and paste tests start from.
/// </summary>
internal static class ClipboardSetup
{
    /// <summary>
    /// The prefab hash of the tree the copy and paste tests use.
    /// </summary>
    internal static readonly int Beech = StableHash.Of("Beech1");

    /// <summary>
    /// The ground height at grid point (x, z).
    /// </summary>
    internal static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

    /// <summary>
    /// A 2 × 2 zone area with a 3 m mound at grid (40, 40) and a tree on it (world = grid - 32).
    /// </summary>
    internal static EditSession Area()
    {
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, Beech, new Vector3(8, 33, 8), new Vector3(0, 30, 0), 1, false));
        Mound(s, 40, 40, 6, 3);
        return s;
    }

    /// <summary>
    /// A main window of this size, shown with the edit session open.
    /// </summary>
    internal static MainWindow WindowOn(EditSession s, double width, double height)
    {
        var w = new MainWindow(load: false) { Width = width, Height = height };
        w.Show();
        w.View.Show(s.Scene, null);
        w.Edit(s);
        return w;
    }

    /// <summary>
    /// The world position of grid point (0, 0) of the area: the offset that turns grid points into world ones.
    /// </summary>
    internal static (float Ox, float Oz) GridOrigin(EditSession s) => (s.Scene.X0 * 64f - 32f, s.Scene.Z0 * 64f - 32f);

    /// <summary>
    /// Raises a round mound of this radius and height, centred on grid point (x, z).
    /// </summary>
    internal static void Mound(EditSession s, int x, int z, float radius, float height) => s.Shape(x, z, Formula.Compile("h * smooth(1 - d / r)", ["d", "r", "h"]), radius, height, "mound");

    /// <summary>
    /// Area() with a wall on the mound's top, beside the tree (thing 1).
    /// </summary>
    internal static EditSession Walled()
    {
        var s = Area();
        s.Scene.Things.Add(new WorldScene.Thing(2, StableHash.Of("woodwall"), new Vector3(8, H(s, 40, 40) + 1, 8), Vector3.Zero, 0, true));
        return s;
    }

    /// <summary>
    /// A copy of one point with no ground of its own, holding the object and the outline given.
    /// </summary>
    /// <param name="obj">The object the copy holds.</param>
    /// <param name="outline">The outline, relative to the middle.</param>
    /// <param name="name">The blueprint's name, if any.</param>
    internal static CopyData SingleObjectClip(CopyData.Obj obj, IEnumerable<Vector2> outline, string? name = null) => new()
    {
        W = 1,
        H = 1,
        Rel = [float.NaN],
        Wt = [0f],
        Pnt = [-1f, -1, -1, -1],
        Objects = [obj],
        Poly = [.. outline],
        Name = name,
    };

    /// <summary>
    /// A copy of a flat ground patch of this size with no objects, holding the outline given.
    /// </summary>
    /// <param name="w">The copy's width in grid points.</param>
    /// <param name="h">The copy's depth in grid points.</param>
    /// <param name="outline">The outline, relative to the middle.</param>
    /// <param name="name">The blueprint's name, if any.</param>
    internal static CopyData GroundOnlyClip(int w, int h, IEnumerable<Vector2> outline, string? name = null) => new()
    {
        W = w,
        H = h,
        Rel = [0f],
        Wt = [1f],
        Pnt = [-1f, -1, -1, -1],
        Objects = [],
        Poly = [.. outline],
        Name = name,
    };

    /// <summary>
    /// Rounds to 3 decimals (with -0 made 0), to compare points.
    /// </summary>
    internal static Vector2 Round(Vector2 v) => new(MathF.Round(v.X, 3) + 0f, MathF.Round(v.Y, 3) + 0f);

    /// <summary>
    /// Copies a box of an open window's area with the keys: the Area tool (B), the box between two grid corners, Ctrl+C.
    /// </summary>
    internal static void CopyBox(MainWindow w, Vector2 from, Vector2 to)
    {
        w.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
        w.View.Area.Points.Add(from);
        w.View.Area.Points.Add(to);
        w.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
    }
}
