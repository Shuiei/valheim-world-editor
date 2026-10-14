using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Picking: the ray through the mouse, against boxes and the ground.
/// </summary>
public class PickingTests
{
    /// <summary>
    /// The view-projection of a camera at <c>eye</c> looking at <c>target</c> (60° field of view, 1.5 aspect).
    /// </summary>
    private static Matrix4x4 Camera(Vector3 eye, Vector3 target)
    {
        return Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY) * GlView.Perspective(MathF.PI / 3, 1.5f, 0.5f, 6000);
    }

    [Fact]
    public void TheRayThroughTheMiddleGoesToTheTarget()
    {
        var (o, d) = Picking.Ray(Camera(new Vector3(0, 10, 10), Vector3.Zero), 0, 0);
        var toTarget = Vector3.Normalize(Vector3.Zero - new Vector3(0, 10, 10));
        Assert.True(Vector3.Distance(d, toTarget) < 1e-3f, $"{d}");
        // It starts on the near plane, half a metre in front of the camera.
        Assert.True(MathF.Abs(Vector3.Distance(o, new Vector3(0, 10, 10)) - 0.5f) < 0.01f, $"{o}");
    }

    [Fact]
    public void BoxesAreHitWhereTheRayEnters()
    {
        var o = new Vector3(0, 0, 10);
        var d = new Vector3(0, 0, -1);
        Assert.Equal(9f, Picking.HitBox(o, d, new Vector3(-1, -1, -1), new Vector3(1, 1, 1))!.Value, 4);
        Assert.Null(Picking.HitBox(o, d, new Vector3(2, -1, -1), new Vector3(3, 1, 1)));
        Assert.Null(Picking.HitBox(o, -d, new Vector3(-1, -1, -1), new Vector3(1, 1, 1)));
    }

    [Fact]
    public void ATurnedBoxGetsTheBoxAroundIt()
    {
        var (lo, hi) = Picking.Transform(new Vector3(-1, 0, -1), new Vector3(1, 2, 1), Matrix4x4.CreateRotationY(MathF.PI / 4) * Matrix4x4.CreateTranslation(10, 0, 0));
        Assert.Equal(10 - MathF.Sqrt(2), lo.X, 3);
        Assert.Equal(10 + MathF.Sqrt(2), hi.X, 3);
        Assert.Equal(2, hi.Y, 3);
    }

    [Fact]
    public void TheGroundIsHitWhereTheRayMeetsIt()
    {
        // A flat ground at 5 m, 65 × 65 points around 0.
        var s = new WorldScene
        {
            World = null!,
            Name = "test",
            W = 65,
            H = 65,
            ZonesX = 1,
            ZonesZ = 1,
            Heights = [.. Enumerable.Repeat(5f, 65 * 65)],
            Biomes = new int[65 * 65],
            Things = [],
            BiomeColor = new byte[65 * 65 * 4],
            Mask = new byte[65 * 65 * 4],
            OceanDepth = new float[65 * 65],
            Limit = new float[65 * 65],
        };
        Assert.Equal(5f, Picking.HeightAt(s, 3.3f, -7.1f), 4);
        float t = Picking.HitGround(s, new Vector3(0, 25, 0), new Vector3(0, -1, 0))!.Value;
        Assert.Equal(20f, t, 2);
        Assert.Null(Picking.HitGround(s, new Vector3(0, 25, 0), new Vector3(0, 1, 0)));
        // A ray that leaves the area above the ground hits nothing (it hit 1000 m down past the edge).
        Assert.Null(Picking.HitGround(s, new Vector3(0, 25, 0), Vector3.Normalize(new Vector3(1, -0.3f, 0))));
    }

    // A ray straight down on flat ground hits it; where a mod's extension cuts the ground (WorldScene.Cut), it goes
    // through: no ground is hit there (nor past the area).
    [Fact]
    public void ARayGoesThroughCutGround()
    {
        var s = TestScenes.Flat(2).Scene;
        var down = new Vector3(0, -1, 0);
        Assert.Equal(70, Picking.HitGround(s, new Vector3(0, 100, 0), down)!.Value, 1);
        Assert.False(Picking.CutAt(s, 0, 0));

        s.Cut = new float[s.W * s.H];
        s.Cut[((s.H - 1) / 2 * s.W) + ((s.W - 1) / 2)] = 1;

        Assert.True(Picking.CutAt(s, 0, 0));
        // Between a cut point and uncut ones, as the shader blends: cut up to half way, ground past it.
        Assert.True(Picking.CutAt(s, 0.4f, 0));
        Assert.False(Picking.CutAt(s, 0.6f, 0));
        Assert.False(Picking.CutAt(s, 3, 3));
        Assert.Null(Picking.HitGround(s, new Vector3(0, 100, 0), down));
        Assert.Equal(70, Picking.HitGround(s, new Vector3(3, 100, 3), down)!.Value, 1);
        // A slanting ray into the hole and out of it below the ground: no ground met on the hole's far side; one
        // that reaches the ground just past the hole's edge, after passing over the cut, meets it there.
        Assert.Null(Picking.HitGround(s, new Vector3(-0.2f, 30.2f, 0), Vector3.Normalize(new Vector3(1, -1, 0)), 20));
        float? past = Picking.HitGround(s, new Vector3(-0.4f, 30.1f, 0), Vector3.Normalize(new Vector3(10, -1, 0)), 20);
        Assert.NotNull(past);
        float x = -0.4f + past.Value * 10 / MathF.Sqrt(101);
        Assert.InRange(x, 0.5f, 0.7f);
    }
}
