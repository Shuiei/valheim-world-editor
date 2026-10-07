using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Mask: brushes, paths, shapes and area actions only change ground that matches it.
public class MaskTests
{
	private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

	// Flat at 30 m, with the east half (x >= 64) Black Forest and the west Meadows.
	private static EditSession Area()
	{
		var s = EditTests.Flat(2);
		for (int i = 0; i < s.Scene.Biomes.Length; i++)
		{
			s.Scene.Biomes[i] = i % s.Scene.W >= 64 ? 8 : 1;
		}
		return s;
	}

	[Fact]
	public void BiomesLimitABrush()
	{
		var s = Area();
		s.Mask.On = true;
		s.Mask.Biomes.Add(8);
		s.Brush.Radius = 10;
		s.BeginStroke(BrushTool.Raise, 64, 64);
		for (int i = 0; i < 20; i++)
		{
			s.StrokeStep(64, 64, 0.1f);
		}
		Assert.Equal("", s.EndStroke());
		Assert.True(H(s, 67, 64) > 30);
		Assert.Equal(30, H(s, 61, 64), 4);
	}

	[Fact]
	public void TheHeightRangeIsJudgedAsTheStrokeStarted()
	{
		var s = Area();
		s.Mask.On = true;
		s.Mask.HeightMax = 31;
		s.Brush.Strength = 1;
		s.BeginStroke(BrushTool.Raise, 64, 64);
		for (int i = 0; i < 30; i++)
		{
			s.StrokeStep(64, 64, 0.1f);
		}
		s.EndStroke();
		// It kept rising past 31 m: the ground was below it when the stroke started.
		Assert.True(H(s, 64, 64) > 35);
	}

	[Fact]
	public void AStrokeTheMaskStopsSaysSo()
	{
		var s = Area();
		s.Mask.On = true;
		s.Mask.HeightMin = 100;
		s.BeginStroke(BrushTool.Raise, 64, 64);
		s.StrokeStep(64, 64, 0.1f);
		Assert.StartsWith("Nothing changed: the Mask", s.EndStroke());
	}

	[Fact]
	public void SlopeAndPaintRules()
	{
		var s = Area();
		s.Shape(30, 30, Formula.Compile("d < 3 ? 6 : 0", new[] { "d" }), 3, 0, "step");
		var m = new Mask { On = true, SlopeMin = 30 };
		var f = m.For(s.Ground, s.Scene.Biomes)!;
		Assert.Equal(1, f(30 * s.Ground.W + 33));
		Assert.Equal(0, f(30 * s.Ground.W + 30));
		Assert.Equal(0, f(100 * s.Ground.W + 100));
		var path = new PathTool { Act = PathTool.Action.PaintPaved, Width = 4, Soft = 0 };
		path.Points.Add(new Vector2(10, 100));
		path.Points.Add(new Vector2(60, 100));
		s.EditGround("p", g => { var (t, r, _) = path.Apply(g, s.Brush, 30); return (t, r); });
		m = new Mask { On = true, Paint = Mask.PaintRule.Paved };
		f = m.For(s.Ground, s.Scene.Biomes)!;
		Assert.Equal(1, f(100 * s.Ground.W + 30));
		Assert.Equal(0, f(110 * s.Ground.W + 30));
		Assert.Equal("Slope min is above max: nothing matches.", new Mask { SlopeMin = 40, SlopeMax = 20 }.Warning());
		Assert.Null(new Mask().For(s.Ground, s.Scene.Biomes));
	}

	[Fact]
	public void ShapesPathsAndAreasFollowTheMaskToo()
	{
		var s = Area();
		s.Mask.On = true;
		s.Mask.Biomes.Add(1);
		s.Shape(64, 20, Formula.Compile("2", new string[0]), 8, 0, "x");
		Assert.Equal(32, H(s, 60, 20), 3);
		Assert.Equal(30, H(s, 68, 20), 3);
		var a = new AreaTool { Soft = 0 };
		a.Points.Add(new Vector2(50, 50));
		a.Points.Add(new Vector2(80, 80));
		s.EditGround("a", g => a.Apply(g, s.Brush, AreaTool.GroundAction.Raise, 0, 1, new float[4], s.MaskNow()));
		Assert.Equal(31, H(s, 60, 60), 3);
		Assert.Equal(30, H(s, 70, 60), 3);
	}

	[AvaloniaFact]
	public void ThePanelAndAltClicks()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = Area();
		w.View.Show(s.Scene, null);
		w.Edit(s);
		Assert.False(w.MaskPanel.Card.IsVisible);
		w.KeyPress(Key.D1, RawInputModifiers.None, PhysicalKey.Digit1, "1");
		Assert.True(w.MaskPanel.Card.IsVisible);
		w.MaskPanel.OnBox.IsChecked = true;
		w.MaskPanel.BiomeButtons[8].IsChecked = true;
		w.MaskPanel.SlopeMax.Value = 0;
		Assert.True(s.Mask.On);
		Assert.Contains(8, s.Mask.Biomes);
		Assert.StartsWith("Slope max 0°", w.MaskPanel.WarningText.Text);
		w.MaskPanel.OnBox.IsChecked = false;
		Assert.False(s.Mask.On);
		// Alt + click: flatten to there; Alt + Shift + click: the mask's height range.
		w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1.6f);
		var q = Vector4.Transform(new Vector4(0, 30, 0, 1), w.View.ViewProj);
		var at = new Avalonia.Point((q.X / q.W + 1) / 2 * 1600, (1 - q.Y / q.W) / 2 * 1000);
		w.MouseDown(at, MouseButton.Left, RawInputModifiers.Alt);
		w.MouseUp(at, MouseButton.Left, RawInputModifiers.Alt);
		Assert.Equal(BrushTool.Flatten, w.Tools.Tool);
		Assert.Equal(30, w.Tools.Brush.Target, 2);
		Assert.False(w.Tools.Brush.TargetFromClick);
		w.MouseDown(at, MouseButton.Left, RawInputModifiers.Alt | RawInputModifiers.Shift);
		w.MouseUp(at, MouseButton.Left, RawInputModifiers.Alt | RawInputModifiers.Shift);
		Assert.True(s.Mask.On);
		Assert.Equal(28, s.Mask.HeightMin!.Value, 2);
		Assert.Equal(32, (double)w.MaskPanel.HeightMax.Value!, 2);
		Assert.Equal(0, s.Pending.Zones);
	}
}
