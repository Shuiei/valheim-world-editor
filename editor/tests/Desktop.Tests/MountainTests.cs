using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The mountain tool: its preview on the ground and where it fits, the seeds and kinds, the rise of every kind inside its
/// reach, and what a mountain takes away when it goes past the limit.
/// </summary>
public class MountainTests
{
    private static readonly MountainSpec Peak = new(MountainKind.Peak, 80, 60, 0.6f, 0, 42);

    // The preview before clicking: the mountain's mesh on the ground under the pointer, only where it
    // rises, as high as it will be; and where it fits in the open area (else the ring is red).
    [Fact]
    public void ThePreviewIsTheMountainOnTheGroundAndSaysWhereItFits()
    {
        var scene = TestScenes.Flat(5).Scene;
        int c = (scene.W - 1) / 2;
        float ground = scene.Heights[c * scene.W + c];
        var m = Peak with { Radius = 50 };
        float[] mesh = GlView.MountainMesh(scene, c, c, m.Reach, Mountain.Shape(m));
        Assert.NotEmpty(mesh);
        Assert.Equal(0, mesh.Length % 6);
        var ys = Enumerable.Range(0, mesh.Length / 3).Select(i => mesh[i * 3 + 1]).ToList();
        Assert.InRange(ys.Max(), ground + m.Height * 0.7f, ground + m.Height * 1.3f);
        Assert.True(ys.Min() >= ground);
        // Every point within the mountain's reach of the middle (view space: x east, z mirrored).
        for (int i = 0; i < mesh.Length / 3; i++)
        {
            float x = mesh[i * 3] + (scene.W - 1) / 2f - c, z = -mesh[i * 3 + 2] + (scene.H - 1) / 2f - c;
            Assert.True(MathF.Abs(x) <= m.Reach + 0.01f && MathF.Abs(z) <= m.Reach + 0.01f);
        }
        Assert.True(GlView.MountainFits(scene, c, c, m.Reach));
        Assert.False(GlView.MountainFits(scene, 20, c, m.Reach));
        Assert.False(GlView.MountainFits(scene, c, scene.H - 10, m.Reach));
    }

    [AvaloniaFact]
    public void TheMountainToolShowsItsPreviewAndOtherToolsDoNot()
    {
        var w = new MainWindow(load: false) { Width = 1400, Height = 900 };
        w.Show();
        var s = TestScenes.Flat(5);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseMode(ToolMode.Mountain);
        var first = w.View.MountainPreview;
        Assert.NotNull(first);
        // A new mountain (Randomize): a new preview.
        w.MountainPanel.Randomize();
        Assert.NotSame(first, w.View.MountainPreview);
        w.Tools.ChooseMode(ToolMode.Shape);
        Assert.Null(w.View.MountainPreview);
        w.Close();
    }

    [Fact]
    public void TheSameSeedMakesTheSameMountainAndAnotherSeedAnother()
    {
        var a = Mountain.Shape(Peak);
        var b = Mountain.Shape(Peak);
        var c = Mountain.Shape(Peak with { Seed = 43 });
        Assert.Equal(a(10, -7), b(10, -7));
        Assert.NotEqual(a(10, -7), c(10, -7));
    }

    [Theory]
    [InlineData(MountainKind.Peak)]
    [InlineData(MountainKind.Ridge)]
    [InlineData(MountainKind.Range)]
    [InlineData(MountainKind.Mesa)]
    [InlineData(MountainKind.Volcano)]
    [InlineData(MountainKind.Hills)]
    public void EveryKindRisesInsideItsReachAndNothingBeyond(MountainKind kind)
    {
        var m = Peak with { Kind = kind };
        var f = Mountain.Shape(m);
        float top = 0;
        for (int z = -100; z <= 100; z += 2)
        {
            for (int x = -100; x <= 100; x += 2)
            {
                float v = f(x, z);
                Assert.True(v >= 0, $"{kind} digs at {x}, {z}");
                if (MathF.Sqrt(x * x + z * z) > m.Reach + 1)
                {
                    Assert.True(v < 0.01f, $"{kind} reaches {x}, {z}: {v}");
                }
                top = MathF.Max(top, v);
            }
        }
        Assert.InRange(top, m.Height * 0.3f, m.Height * 1.35f);
    }

    [Fact]
    public void AVolcanoHasACraterAndAMesaAFlatTop()
    {
        var volcano = Mountain.Shape(Peak with { Kind = MountainKind.Volcano, Rough = 0 });
        Assert.True(volcano(0, 0) < volcano(12, 0), "the crater is lower than its rim");
        var mesa = Mountain.Shape(Peak with { Kind = MountainKind.Mesa, Rough = 0.6f });
        Assert.InRange(mesa(0, 0) - mesa(8, 5), -1, 1);
        Assert.Equal(80, mesa(0, 0), 1);
    }

    [Fact]
    public void PresetsRollSizesWithinTheirRanges()
    {
        var r = new Random(7);
        foreach (var p in Mountain.Presets)
        {
            for (int i = 0; i < 20; i++)
            {
                var m = Mountain.Roll(p, r);
                Assert.Equal(p.Kind, m.Kind);
                Assert.InRange(m.Height, p.Height.Min, p.Height.Max);
                Assert.InRange(m.Radius, p.Radius.Min, p.Radius.Max);
            }
        }
    }

    [Fact]
    public void AMountainGoesPastTheLimitAndTakesAwayWhatItBuries()
    {
        var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(-32 + 160, 30, -32 + 160), Vector3.Zero, 0, false);
        var wall = new WorldScene.Thing(6, StableHash.Of("stone_wall_4x2"), new Vector3(-32 + 165, 30, -32 + 160), Vector3.Zero, 0, true);
        var s = TestScenes.Flat(5, tree, wall);
        var done = s.Mountain(160, 160, Peak, clear: true, "Mountain: test");
        Assert.NotNull(done);
        int g = 160 * s.Ground.W + 160;
        Assert.True(s.Ground.HeightOf(g) > 30 + 50, $"{s.Ground.HeightOf(g)}");
        Assert.True(s.Ground.Lift[g] > 40);
        // The No limit switch is as it was.
        Assert.False(s.Ground.NoLimit);
        Assert.Equal(1, done.Value.Cleared);
        Assert.True(s.Scene.Things[0].Gone);
        Assert.False(s.Scene.Things[1].Gone);
        // One undo step takes it all back.
        Assert.True(s.Undo());
        Assert.Equal(30, s.Ground.HeightOf(g), 3);
        Assert.False(s.Scene.Things[0].Gone);
        // Too big for the open area: nothing happens.
        Assert.Null(s.Mountain(20, 20, Peak, clear: true, "Mountain: test"));
    }
}
