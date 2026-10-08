using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The web editor's view features in the native app: the status bar's cursor readout (world x/z, the
// ground against the original and the ±8 m limit, the zone), and the View panel's Look section (game
// look on/off, see-through buildings, 3D resolution) and presets (Defaults, All, Ground).
[Collection("World files")]
public class ParityViewTests
{
	// ---- The cursor readout.

	[Fact]
	public void TheReadoutGivesWorldMetresTheGroundAndTheZone()
	{
		var s = EditTests.Flat(2);
		// Grid (64, 64) of an area from zone (0, 0): world x = -32 + 64 = 32, in zone 1.
		Assert.Equal("x 32, z 32   ground 30.00 m (original 30.00, +0.00)   zone 1, 1", CursorReadout.Text(s.Scene, 64.4f, 63.6f));
		Assert.Equal("x -32, z -32   ground 30.00 m (original 30.00, +0.00)   zone 0, 0", CursorReadout.Text(s.Scene, 0, 0));
		// Zone edges: -32 is in zone 0, 31 still in it, 32 in the next.
		Assert.EndsWith("zone 0, 0", CursorReadout.Text(s.Scene, 63, 63));
		Assert.EndsWith("zone 1, 0", CursorReadout.Text(s.Scene, 64, 63));
	}

	[Fact]
	public void PointsOutsideTheAreaReadTheNearestEdge()
	{
		var s = EditTests.Flat(2);
		Assert.StartsWith("x -32, z 96", CursorReadout.Text(s.Scene, -20, 500));
	}

	[Fact]
	public void RaisedLoweredAndLimitedGroundIsShownAgainstTheOriginal()
	{
		var s = EditTests.Flat(2);
		var g = s.Ground;
		int p = 64 * s.Scene.W + 64;
		g.Mod[p] = 1;
		g.Level[p] = 2.5f;
		Assert.Contains("ground 32.50 m (original 30.00, +2.50)", CursorReadout.Text(s.Scene, 64, 64));
		g.Level[p] = -1.25f;
		Assert.Contains("ground 28.75 m (original 30.00, -1.25)", CursorReadout.Text(s.Scene, 64, 64));
		// At the game's limit: the height stops at 8 m, and the readout says so.
		g.Level[p] = 9;
		Assert.Contains("ground 38.00 m (original 30.00, +8.00, at the ±8 m limit)", CursorReadout.Text(s.Scene, 64, 64));
		g.Level[p] = -8;
		Assert.Contains("(original 30.00, -8.00, at the ±8 m limit)", CursorReadout.Text(s.Scene, 64, 64));
	}

	[Fact]
	public void TheReadoutUsesDotsWhateverTheLanguage()
	{
		var old = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
			Assert.Contains("ground 30.00 m", CursorReadout.Text(EditTests.Flat(1).Scene, 10, 10));
		}
		finally
		{
			CultureInfo.CurrentCulture = old;
		}
	}

	[Fact]
	public void AnAreaWithoutEditingShowsOnlyItsHeight()
	{
		var s = EditTests.Flat(1).Scene;
		s.Session = null;
		Assert.Equal("x -22, z -12   ground 30.00 m   zone 0, 0", CursorReadout.Text(s, 10, 20));
	}

	private static (MainWindow W, EditSession S, Func<float, float, Avalonia.Point> Screen) Open(params WorldScene.Thing[] things)
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2, things);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1);
		var size = w.View.Bounds.Size;
		Avalonia.Point Screen(float gx, float gz)
		{
			var q = Vector4.Transform(new Vector4(gx - 64, 30, -(gz - 64), 1), w.View.ViewProj);
			return new Avalonia.Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
		}
		return (w, s, Screen);
	}

	[AvaloniaFact]
	public void TheStatusBarFollowsTheMouseOverTheGround()
	{
		var (w, s, screen) = Open();
		Assert.True(string.IsNullOrEmpty(w.Cursor.Text));
		w.MouseMove(screen(64, 64));
		Assert.StartsWith("x 32, z 32   ground 30.00 m", w.Cursor.Text);
		w.MouseMove(screen(10, 100));
		Assert.StartsWith("x -22, z 68", w.Cursor.Text);
		// The ground changes under a still mouse: shown again (after a stroke, for one).
		s.Ground.Mod[100 * s.Scene.W + 10] = 1;
		s.Ground.Level[100 * s.Scene.W + 10] = 1;
		w.ShowCursor(screen(10, 100));
		Assert.Contains("+1.00", w.Cursor.Text);
		// Off the view: nothing.
		w.ShowCursor(null);
		Assert.Equal("", w.Cursor.Text);
	}

	[AvaloniaFact]
	public void WithoutAnAreaTheReadoutStaysEmpty()
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
		w.Show();
		w.ShowCursor(new Avalonia.Point(500, 500));
		Assert.Equal("", w.Cursor.Text);
	}

	// ---- View: Look.

	[AvaloniaFact]
	public void TheLookSwitchesReachTheView()
	{
		var (w, _, _) = Open();
		Assert.True(w.View.GameLookOn);
		Assert.True(w.GameLookBox.IsChecked);
		w.GameLookBox.IsChecked = false;
		Assert.False(w.View.GameLookOn);
		w.GameLookBox.IsChecked = true;
		Assert.True(w.View.GameLookOn);
		Assert.False(w.View.SeeThroughBuildings);
		w.SeeThroughBox.IsChecked = true;
		Assert.True(w.View.SeeThroughBuildings);
		w.SeeThroughBox.IsChecked = false;
		Assert.False(w.View.SeeThroughBuildings);
		// Without a copied game look in the tests, it is not loaded.
		Assert.False(w.View.GameLookLoaded);
	}

	[AvaloniaFact]
	public void TheResolutionIsChosenAndSaysItsPixels()
	{
		var (w, _, _) = Open();
		Assert.Equal(GlView.Resolution.Sharp, w.View.Resolution3D);
		var full = w.View.RenderSize();
		w.ResolutionBox.SelectedIndex = 2;
		Assert.Equal(GlView.Resolution.Fast, w.View.Resolution3D);
		var fast = w.View.RenderSize();
		Assert.Equal((int)(w.View.Bounds.Width * 0.75), fast.W);
		Assert.Equal((int)(w.View.Bounds.Height * 0.75), fast.H);
		Assert.True(fast.W < full.W);
		Assert.Equal($"3D resolution: Fast, {fast.W}×{fast.H} pixels.", w.MessageText.Text);
		w.ResolutionBox.SelectedIndex = 1;
		Assert.Equal(GlView.Resolution.Balanced, w.View.Resolution3D);
		Assert.StartsWith("3D resolution: Balanced, ", w.MessageText.Text);
		w.ResolutionBox.SelectedIndex = 0;
		Assert.Equal(full, w.View.RenderSize());
	}

	[Theory]
	[InlineData(GlView.Resolution.Sharp, 1, 1)]
	[InlineData(GlView.Resolution.Sharp, 2, 2)]
	[InlineData(GlView.Resolution.Balanced, 2, 1)]
	[InlineData(GlView.Resolution.Balanced, 0.8, 0.8)]
	[InlineData(GlView.Resolution.Fast, 2, 0.75)]
	[InlineData(GlView.Resolution.Fast, 1, 0.75)]
	[InlineData(GlView.Resolution.Fast, 0.8, 0.6)]
	public void EachResolutionDrawsSoManyPixelsPerPoint(GlView.Resolution r, double screen, double expected) => Assert.Equal(expected, GlView.PixelScale(r, screen), 6);

	[AvaloniaFact]
	public void AViewOfNoSizeStillDrawsAPixel()
	{
		var v = new GlView();
		v.Resolution3D = GlView.Resolution.Fast;
		Assert.Equal((1, 1), v.RenderSize());
	}

	[AvaloniaFact]
	public void SwitchingTheGameLookTwiceTheSameWayChangesNothing()
	{
		var v = new GlView();
		v.GameLookOn = true;
		Assert.True(v.GameLookOn);
		v.GameLookOn = false;
		v.GameLookOn = false;
		Assert.False(v.GameLookOn);
	}

	// ---- View: presets.

	[AvaloniaFact]
	public void PresetsShowEverythingOnlyTheGroundOrTheDefaults()
	{
		var (w, _, _) = Open();
		var boxes = w.KindBoxes.Values.Append(w.WaterBox).Concat(w.OverlayBoxes.Values).ToList();
		var defaults = boxes.Select(b => b.IsChecked == true).ToList();
		// Some on and some off by default.
		Assert.Contains(true, defaults);
		Assert.Contains(false, defaults);
		w.AllButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Assert.All(boxes, b => Assert.True(b.IsChecked));
		Assert.All(ObjectKinds.All, k => Assert.True(w.View.IsShown(k)));
		Assert.True(w.View.ShowWater);
		Assert.All(w.OverlayBoxes.Keys, l => Assert.True(w.View.IsOverlayShown(l)));
		w.GroundButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Assert.All(boxes, b => Assert.False(b.IsChecked));
		Assert.All(ObjectKinds.All, k => Assert.False(w.View.IsShown(k)));
		Assert.False(w.View.ShowWater);
		w.DefaultsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Assert.Equal(defaults, boxes.Select(b => b.IsChecked == true).ToList());
	}

	[AvaloniaFact]
	public void PresetsLeaveTheLookAlone()
	{
		var (w, _, _) = Open();
		w.SeeThroughBox.IsChecked = true;
		w.SlopeBox.IsChecked = true;
		w.ShowPreset(MainWindow.ViewPreset.Ground);
		w.ShowPreset(MainWindow.ViewPreset.Defaults);
		Assert.True(w.GameLookBox.IsChecked);
		Assert.True(w.SeeThroughBox.IsChecked);
		Assert.True(w.SlopeBox.IsChecked);
	}

	[AvaloniaFact]
	public void TheLookSectionComesFirstInTheViewPanel()
	{
		var (w, _, _) = Open();
		var texts = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<TextBlock>().Select(t => t.Text).ToList();
		int look = texts.IndexOf("LOOK"), objects = texts.IndexOf("OBJECTS"), ground = texts.IndexOf("GROUND (GAME LOOK)");
		Assert.True(look >= 0 && objects > look && ground > objects, $"{look} {objects} {ground}");
		Assert.Contains("3D resolution", texts);
	}
}
