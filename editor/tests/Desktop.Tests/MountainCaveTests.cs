using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The cave of the Path tool as the Cave button of the rail opens it: a trench deep in the middle with entrances at its ends,
/// its roof covering it, and the Mountain button beside it.
/// </summary>
public class MountainCaveTests
{
    /// <summary>
    /// A straight 100 m Cave line across the area (8 m wide, fixed seed), with the given depth and headroom in metres.
    /// </summary>
    private static PathTool CaveLine(float depth = 7.5f, float headroom = 4.5f)
    {
        var path = new PathTool { Act = PathTool.Action.Cave, Width = 8, Soft = 2.5f, CaveDepth = depth, Headroom = headroom, Seed = 3, Curved = false };
        path.Points.Add(new Vector2(10, 60));
        path.Points.Add(new Vector2(110, 60));
        return path;
    }

    [Fact]
    public void ACaveIsATrenchDeepInTheMiddleWithEntrancesAtItsEnds()
    {
        var s = TestScenes.Flat(2);
        var path = CaveLine();
        var plan = path.PlanCave(s.Ground);
        var (touched, _, clamped) = path.Apply(s.Ground, s.Brush, 30, null, plan);
        Assert.NotEmpty(touched);
        Assert.False(clamped);
        float H(int x, int z) => s.Ground.HeightOf(z * s.Ground.W + x);
        Assert.Equal(30 - 7.5f, H(60, 60), 0.2f);
        // Its ends slope up to the ground; beside it the ground is as it was.
        Assert.True(H(12, 60) > 30 - 2);
        Assert.Equal(30, H(60, 70), 3);
        // Within the game's ±8 m.
        Assert.All(touched, p => Assert.False(s.Ground.AtLimit(p)));
    }

    [Fact]
    public void TheRoofCoversTheCaveClearsTheHeadroomAndTheWallsRiseIntoIt()
    {
        var s = TestScenes.Flat(2);
        var path = CaveLine();
        var plan = path.PlanCave(s.Ground);
        var roof = CaveRoof.Build(path, plan, s.Ground.W, s.Ground.H, -32, -32, (_, _) => PathTool.CaveRock.Auto);
        Assert.True(roof.Rocks.Count >= 4, $"{roof.Rocks.Count} boulders");
        Assert.Equal(0, roof.Uncovered);
        float ceiling = 30 - 7.5f + 4.5f;
        var sizes = new HashSet<float>();
        foreach (var r in roof.Rocks)
        {
            Assert.Equal("rock4_forest", r.Prefab);
            sizes.Add(r.Scale);
            // Its real underside clears the ceiling over the floor, and touches it somewhere.
            var low = CaveRoof.Underside(r.Prefab, r.Position.X + 32, r.Position.Z + 32, r.Rotation, r.Scale, s.Ground.W, s.Ground.H);
            var overFloor = low.Where(kv => MathF.Abs(kv.Key / s.Ground.W - 60) <= path.Width / 2 && kv.Key % s.Ground.W is > 30 and < 90)
                .Select(kv => kv.Value + r.Position.Y).ToList();
            if (overFloor.Count > 0)
            {
                Assert.True(overFloor.Min() >= ceiling - 0.6f, $"{overFloor.Min()} under the ceiling {ceiling}");
            }
        }
        // Turned and sized at random.
        Assert.True(sizes.Count > 1);
        Assert.True(roof.Rocks.Select(r => MathF.Round(r.Rotation.Y)).Distinct().Count() > 1);
        // The walls rise to the rock where it is above them; never over the floor.
        Assert.NotEmpty(roof.Seal);
        Assert.All(roof.Seal.Keys, p => Assert.True(MathF.Abs(p / s.Ground.W - 60f) > path.Width / 2 - 0.01f));
        // Upside down, their undersides are nearly level: the walls stay within the game's ±8 m.
        Assert.All(roof.Seal.Values, v => Assert.True(v <= 30 + 8.5f, $"a wall to {v}"));
        // The same seed, the same roof; a shallow cave has none.
        Assert.Equal(roof.Rocks, CaveRoof.Build(path, plan, s.Ground.W, s.Ground.H, -32, -32, (_, _) => PathTool.CaveRock.Auto).Rocks);
        var shallow = CaveLine(depth: 4, headroom: 4);
        Assert.Empty(CaveRoof.Build(shallow, shallow.PlanCave(s.Ground), s.Ground.W, s.Ground.H, -32, -32, (_, _) => PathTool.CaveRock.Auto).Rocks);
    }

    /// <summary>
    /// Raises the button's click (no posted work run).
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// The rail's button showing this name.
    /// </summary>
    private static Button RailButton(MainWindow w, string name) => w.Tools.Rail.GetLogicalDescendants().OfType<Button>()
        .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == name));

    [AvaloniaFact]
    public void TheCaveButtonOpensThePathToolOnItsCaveAction()
    {
        var s = TestScenes.Flat(2);
        var w = TestWindows.Editing(s);
        Click(RailButton(w, "Cave"));
        Assert.Equal(ToolMode.Path, w.Tools.Mode);
        Assert.Equal(PathTool.Action.Cave, w.View.Path.Act);
        Assert.True(w.PathPanel.CaveDepthBox.IsEffectivelyVisible);
        // Randomize changes the sizes around the preset's and the boulders' seed.
        int seed = w.View.Path.Seed;
        Click(w.PathPanel.CaveRandomButton);
        Assert.NotEqual(seed, w.View.Path.Seed);
        Assert.InRange(w.View.Path.Width, 8 * 0.85f - 0.5f, 8 * 1.15f + 0.5f);
        // The Mountain button opens its panel, with a preset rolled.
        Click(RailButton(w, "Mountain"));
        Assert.Equal(ToolMode.Mountain, w.Tools.Mode);
        Assert.True(w.MountainPanel.Card.IsVisible);
        var before = w.MountainPanel.Spec;
        // A 2-zone area: every roll fits in it.
        for (int i = 0; i < 20; i++)
        {
            w.MountainPanel.Randomize();
            Assert.True(w.MountainPanel.Spec.Reach <= (s.Ground.W - 1) / 2f - 2 + 1, $"{w.MountainPanel.Spec.Reach}");
        }
        Click(w.MountainPanel.RandomizeButton);
        Assert.NotEqual(before.Seed, w.MountainPanel.Spec.Seed);
    }
}
