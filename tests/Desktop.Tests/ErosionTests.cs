using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Erosion: slopes settle to their rest angle, rain moves ground downhill.
public class ErosionTests
{
	// A 20 × 20 window with a 10 m cliff in the middle (x >= 10 is high).
	private static (float[] H, float[] Wt) Cliff()
	{
		var h = new float[400];
		var wt = Enumerable.Repeat(1f, 400).ToArray();
		for (int z = 0; z < 20; z++)
		{
			for (int x = 10; x < 20; x++)
			{
				h[z * 20 + x] = 10;
			}
		}
		return (h, wt);
	}

	[Fact]
	public void ThermalSettlesACliffAndKeepsTheGround()
	{
		var (h, wt) = Cliff();
		float sum = h.Sum();
		Erosion.Thermal(h, wt, 20, 20, Erosion.Talus(33), 1, 200);
		// No ground is made or lost, and the step is much less steep.
		Assert.Equal(sum, h.Sum(), 1);
		float step = h[10 * 20 + 10] - h[10 * 20 + 9];
		Assert.True(step < 3, $"{step}");
	}

	[Fact]
	public void PointsWithNoWeightStayPut()
	{
		var (h, wt) = Cliff();
		Array.Fill(wt, 0f);
		Erosion.Thermal(h, wt, 20, 20, Erosion.Talus(33), 1, 50);
		Erosion.Hydraulic(h, wt, 20, 20, 500, 1, new Random(1));
		Assert.Equal(10, h[10 * 20 + 10]);
		Assert.Equal(0, h[10 * 20 + 9]);
	}

	[Fact]
	public void RainCarriesGroundDownhill()
	{
		// A slope rising to the east.
		var h = new float[900];
		for (int i = 0; i < 900; i++)
		{
			h[i] = i % 30 * 0.5f;
		}
		var wt = Enumerable.Repeat(1f, 900).ToArray();
		var before = (float[])h.Clone();
		Erosion.Hydraulic(h, wt, 30, 30, 3000, 1, new Random(3));
		float upper = Enumerable.Range(0, 900).Where(i => i % 30 is > 18 and < 27).Sum(i => h[i] - before[i]);
		Assert.True(upper < 0, $"the upper slope should lose ground: {upper}");
	}

	[Fact]
	public void TheErodeBrushIsOneStroke()
	{
		var s = EditTests.Flat(2);
		s.Shape(64, 64, Formula.Compile("d < 4 ? 6 : 0", new[] { "d" }), 4, 0, "tower");
		float top = s.Scene.Heights[64 * s.Scene.W + 64];
		s.Brush.Radius = 10;
		s.Brush.Strength = 1;
		s.BeginStroke(BrushTool.Erode, 64, 64);
		for (int i = 0; i < 30; i++)
		{
			s.StrokeStep(64, 64, 0.1f);
		}
		s.EndStroke();
		Assert.True(s.Scene.Heights[64 * s.Scene.W + 64] < top - 0.5f);
		Assert.True(s.Scene.Heights[64 * s.Scene.W + 69] > 30.1f);
		Assert.Equal("Erode", s.UndoLabel);
	}

	[AvaloniaFact]
	public async Task TheKeyAndTheAreaAction()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2);
		s.Shape(64, 64, Formula.Compile("d < 4 ? 6 : 0", new[] { "d" }), 4, 0, "tower");
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.KeyPress(Key.O, RawInputModifiers.None, PhysicalKey.O, "o");
		Assert.Equal(BrushTool.Erode, w.Tools.Tool);
		w.Tools.WaterButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		Assert.True(w.Tools.Brush.ErodeWater);
		Assert.False(w.Tools.RestSlider.IsEffectivelyVisible);
		w.Tools.ChooseMode(ToolMode.Area);
		w.View.Area.Points.Add(new Vector2(50, 50));
		w.View.Area.Points.Add(new Vector2(78, 78));
		w.AreaPanel.Choose(AreaPanel.Act.Erode);
		await w.AreaPanel.Apply();
		Assert.True(s.Scene.Heights[64 * s.Scene.W + 64] < 36);
		Assert.Equal("Area: Erode", s.UndoLabel);
	}
}
