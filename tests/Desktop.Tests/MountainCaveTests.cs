using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Mountain tool (shapes made from a seed, past the game's ±8 m) and the Path tool's Cave (a trench
// roofed with the game's boulders), with their presets.
public class MountainCaveTests
{
	private static readonly MountainSpec Peak = new(MountainKind.Peak, 80, 60, 0.6f, 0, 42);

	// The preview before clicking: the mountain's mesh on the ground under the pointer, only where it
	// rises, as high as it will be; and where it fits in the open area (else the ring is red).
	[Fact]
	public void ThePreviewIsTheMountainOnTheGroundAndSaysWhereItFits()
	{
		var scene = EditTests.Flat(5).Scene;
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
		var s = EditTests.Flat(5);
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
		var s = EditTests.Flat(5, tree, wall);
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
		var s = EditTests.Flat(2);
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
		var s = EditTests.Flat(2);
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

	private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

	// The rail's button showing this name.
	private static Button RailButton(MainWindow w, string name) => w.Tools.Rail.GetLogicalDescendants().OfType<Button>()
		.First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == name));

	[AvaloniaFact]
	public void TheCaveButtonOpensThePathToolOnItsCaveAction()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2);
		w.View.Show(s.Scene, null);
		w.Edit(s);
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
