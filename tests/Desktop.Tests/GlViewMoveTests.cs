using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The camera's move steps: a move along the view's flat direction covers the distance the time allows (the orbit's
/// target, at least 10 m for the distance), and with no keys held nothing is held.
/// </summary>
public class GlViewMoveTests
{
    [AvaloniaFact]
    public void MoveByMovesTheOrbitTargetOverTheTime()
    {
        var w = new MainWindow(load: false) { Width = 800, Height = 600 };
        w.Show();
        w.View.Show(TestScenes.FlatScene(), null);
        var v = w.View;
        var before = v.Camera.Target;
        float reach = Math.Max(10, v.Camera.Distance);
        v.MoveBy(new Vector3(0, 0, -1), 0.5f, false);
        var after = v.Camera.Target;
        Assert.Equal(reach * 0.5, Vector2.Distance(new Vector2(before.X, before.Z), new Vector2(after.X, after.Z)), 2);
    }

    [AvaloniaFact]
    public void NoKeysHeldMeansNoMove()
    {
        var w = new MainWindow(load: false) { Width = 800, Height = 600 };
        w.Show();
        w.View.Show(TestScenes.FlatScene(), null);
        Assert.Equal((Vector3.Zero, 0f, false), w.View.HeldKeys());
    }

    [AvaloniaFact]
    public void MoveWithKeysDoesNothingWithNoKeysHeld()
    {
        var w = new MainWindow(load: false) { Width = 800, Height = 600 };
        w.Show();
        w.View.Show(TestScenes.FlatScene(), null);
        Assert.False(w.View.MoveWithKeys(0.1f));
    }

    [Fact]
    public void PerspectiveScalesTheWidthByTheFieldOfViewAndTheAspect()
    {
        var m = GlView.Perspective(MathF.PI / 2, 2, 1, 100);
        // The field of view is 90 degrees: the focal length is 1, so the width scale is 1 over the aspect.
        Assert.Equal(0.5f, m.M11, 4);
        Assert.Equal(1f, m.M22, 4);
        Assert.Equal(-1f, m.M34, 4);
    }
}
