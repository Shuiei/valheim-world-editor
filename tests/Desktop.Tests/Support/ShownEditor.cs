using System.Numerics;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The window the Select and view tests drive: a shown 1000 × 1000 main window editing a flat 2 × 2 zone area with the
/// objects given, and the camera left where the caller puts it.
/// </summary>
internal static class ShownEditor
{
    /// <summary>
    /// A shown main window editing a flat 2 × 2 zone area with these objects, and its edit session; with the screen point
    /// of a point in view space, read against the camera as it is when called.
    /// </summary>
    internal static (MainWindow W, EditSession S, Func<Vector3, Avalonia.Point> Screen) Open(params WorldScene.Thing[] things)
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2, things);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        Avalonia.Point Screen(Vector3 p) => ScreenPoint(w, w.View.Bounds.Size, p);
        return (w, s, Screen);
    }

    /// <summary>
    /// Where a point in view space is on the screen of a view of this size, read against the camera as it is when called.
    /// </summary>
    /// <param name="w">The window whose view the point is read against.</param>
    /// <param name="size">The view's size on the screen.</param>
    /// <param name="p">The point in view space.</param>
    /// <returns>The point on the screen, in pixels from the top left.</returns>
    internal static Avalonia.Point ScreenPoint(MainWindow w, Avalonia.Size size, Vector3 p)
    {
        var q = Vector4.Transform(new Vector4(p, 1), w.View.ViewProj);
        return new Avalonia.Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
    }
}
