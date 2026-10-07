using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Overlays (zone borders, wards, build ranges), and walking and flying over the ground.
public class OverlayTests
{
	// A flat 1 × 1 zone area at 35 m, with the given objects.
	internal static WorldScene Flat(params WorldScene.Thing[] things) => new()
	{
		World = null!, Name = "test", X0 = 0, Z0 = 0, Size = 1, W = 65, H = 65, Heights = Enumerable.Repeat(35f, 65 * 65).ToArray(), Biomes = new int[65 * 65],
		Things = things.ToList(), BiomeColor = new byte[65 * 65 * 4], Mask = new byte[65 * 65 * 4], OceanDepth = new float[65 * 65], Limit = new float[65 * 65],
	};

	[Fact]
	public void BordersFollowTheZoneEdgesOnTheGround()
	{
		var o = Overlays.Build(Flat(), null, _ => null);
		float[] b = o.Lines[Overlays.Layer.Borders];
		Assert.NotEmpty(b);
		// Every point a little above the ground.
		for (int i = 1; i < b.Length; i += 3)
		{
			Assert.Equal(35.15f, b[i], 3);
		}
		Assert.Empty(o.Lines[Overlays.Layer.Wards]);
	}

	[Fact]
	public void AWardAndAWorkbenchGetTheirRings()
	{
		var things = new[]
		{
			new WorldScene.Thing(1, StableHash.Of("guard_stone"), new Vector3(0, 35, 0), Vector3.Zero, 0, true),
			new WorldScene.Thing(2, StableHash.Of("piece_workbench"), new Vector3(10, 35, 10), Vector3.Zero, 0, true),
		};
		string?[] names = { "guard_stone", "piece_workbench" };
		var o = Overlays.Build(Flat(things), null, i => names[i]);
		Assert.Equal(1, o.Wards);
		Assert.Equal(1, o.Stations);
		// The workbench's ring: 20 m round it (view space: the area's middle is 0, z mirrored).
		float[] r = o.Lines[Overlays.Layer.Stations];
		float max = 0;
		for (int i = 0; i < r.Length; i += 3)
		{
			max = MathF.Max(max, MathF.Abs(r[i] - 10));
		}
		Assert.Equal(20, max, 1);
	}

	[AvaloniaFact]
	public void FWalksAtEyeHeightThenFliesThenComesBack()
	{
		var w = new MainWindow(load: false);
		w.Show();
		var v = w.View;
		v.Show(Flat(), null);
		Assert.Equal(GlView.EyeMode.Orbit, v.Eye);
		w.KeyPress(Avalonia.Input.Key.F, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.F, "f");
		Assert.Equal(GlView.EyeMode.Walk, v.Eye);
		Assert.Equal(35 + 1.8f, v.EyePosition.Y, 3);
		// Walking forward keeps the eyes 1.8 m over the ground.
		w.KeyPress(Avalonia.Input.Key.W, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.W, "w");
		var start = v.EyePosition;
		Assert.True(v.Step(0.1f));
		Assert.True(Vector3.Distance(start, v.EyePosition) > 0.3f);
		Assert.Equal(35 + 1.8f, v.EyePosition.Y, 3);
		w.KeyRelease(Avalonia.Input.Key.W, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.W, "w");
		// Flying: Space goes up.
		w.KeyPress(Avalonia.Input.Key.F, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.F, "f");
		Assert.Equal(GlView.EyeMode.Fly, v.Eye);
		w.KeyPress(Avalonia.Input.Key.Space, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Space, " ");
		float y = v.EyePosition.Y;
		v.Step(0.1f);
		Assert.True(v.EyePosition.Y > y + 1);
		w.KeyRelease(Avalonia.Input.Key.Space, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Space, " ");
		w.KeyPress(Avalonia.Input.Key.F, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.F, "f");
		Assert.Equal(GlView.EyeMode.Orbit, v.Eye);
	}

	[AvaloniaFact]
	public void OverlaySwitchesStartWithBordersOnly()
	{
		var w = new MainWindow(load: false);
		w.Show();
		foreach (var (layer, box) in w.OverlayBoxes)
		{
			Assert.Equal(layer == Overlays.Layer.Borders, box.IsChecked);
		}
		w.OverlayBoxes[Overlays.Layer.Flatten].IsChecked = true;
		Assert.True(w.View.IsOverlayShown(Overlays.Layer.Flatten));
		Assert.True(w.View.IsOverlayShown(Overlays.Layer.FlattenEdge));
		w.SlopeBox.IsChecked = true;
		Assert.True(w.View.SlopeColours);
		w.ContourBox.IsChecked = true;
		Assert.Equal(2, w.View.ContourStep);
		w.ContourStepBox.SelectedIndex = 2;
		Assert.Equal(5, w.View.ContourStep);
		w.ContourBox.IsChecked = false;
		Assert.Equal(0, w.View.ContourStep);
	}
}
