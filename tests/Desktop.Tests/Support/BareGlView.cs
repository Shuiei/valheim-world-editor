using System.Numerics;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A plain window holding a view and the transparent surface that takes its mouse, the view on an area and its
/// camera 70 m over the area's middle, looking down.
/// </summary>
internal sealed class BareGlView
{
    /// <summary>
    /// The window's size, the view's (the surface over it fills the window).
    /// </summary>
    internal static readonly Size ViewSize = new(800, 600);

    public Window W { get; }
    public GlView V { get; } = new();
    public WorldScene Scene { get; }
    /// <summary>The transparent surface over the view that takes its mouse.</summary>
    public Border Surface { get; } = new() { Background = Brushes.Transparent };

    public BareGlView(EditSession session)
    {
        Scene = session.Scene;
        W = new Window { Width = ViewSize.Width, Height = ViewSize.Height, Content = new Grid { Children = { V, Surface } } };
        V.Attach(Surface, W);
        W.Show();
        V.Show(Scene, null);
        V.SetCamera(new Vector3(0, 100, 1), new Vector3(0, 30, 0), (float)(ViewSize.Width / ViewSize.Height));
    }

    /// <summary>
    /// Where a point in view space (x east, height, z) is on the window.
    /// </summary>
    public Point At(float x, float y, float z)
    {
        var q = Vector4.Transform(new Vector4(x, y, z, 1), V.ViewProj);
        return new Point((q.X / q.W + 1) / 2 * ViewSize.Width, (1 - q.Y / q.W) / 2 * ViewSize.Height);
    }

    /// <summary>
    /// A left click there, with these keys held.
    /// </summary>
    public void Click(Point p, RawInputModifiers keys = RawInputModifiers.None)
    {
        W.MouseDown(p, MouseButton.Left, keys);
        W.MouseUp(p, MouseButton.Left, keys);
    }

    /// <summary>
    /// A grid point of the 2 × 2 zone test area from its view-space point: (x + 64, 64 − z).
    /// </summary>
    internal static void AssertGrid(float x, float z, (float X, float Z) grid)
    {
        Assert.Equal(x + 64, grid.X, 0.2f);
        Assert.Equal(64 - z, grid.Z, 0.2f);
    }
}
