using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The web editor's keys and mouse in the native app: [ ] brush size, H for the View tool, Space +
// left drag and Shift + right drag sliding the view whatever the tool (and Space never pressing the
// focused button), Alt + wheel turning the selection, the paste and the Place preview as , and . do
// (zooming when there is nothing to turn), the help listing them, and Naturalize's New pattern.
[Collection("World files")]
public class ParityKeysTests
{
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

	private static WorldScene.Thing Wall() => new(11, StableHash.Of("wood_wall_half"), new Vector3(40 - 32, 30, 40 - 32), Vector3.Zero, 0, true);

	// ---- [ ] brush size.

	[AvaloniaFact]
	public void BracketsMakeTheBrushSmallerAndBiggerWithinItsRange()
	{
		var (w, _, _) = Open();
		w.Tools.Choose(BrushTool.Raise);
		w.Tools.SizeSlider.Value = 8;
		w.KeyPress(Key.OemCloseBrackets, RawInputModifiers.None, PhysicalKey.BracketRight, "]");
		Assert.Equal(9, w.Tools.SizeSlider.Value);
		Assert.Equal(9, w.Tools.Brush.Radius);
		w.KeyPress(Key.OemOpenBrackets, RawInputModifiers.None, PhysicalKey.BracketLeft, "[");
		w.KeyPress(Key.OemOpenBrackets, RawInputModifiers.None, PhysicalKey.BracketLeft, "[");
		Assert.Equal(7, w.Tools.Brush.Radius);
		// The ends hold.
		w.Tools.SizeSlider.Value = 1.5;
		w.KeyPress(Key.OemOpenBrackets, RawInputModifiers.None, PhysicalKey.BracketLeft, "[");
		Assert.Equal(1, w.Tools.Brush.Radius);
		w.KeyPress(Key.OemOpenBrackets, RawInputModifiers.None, PhysicalKey.BracketLeft, "[");
		Assert.Equal(1, w.Tools.Brush.Radius);
		w.Tools.SizeSlider.Value = 30;
		w.KeyPress(Key.OemCloseBrackets, RawInputModifiers.None, PhysicalKey.BracketRight, "]");
		Assert.Equal(30, w.Tools.Brush.Radius);
		// With Ctrl, not the brush's.
		w.Tools.SizeSlider.Value = 10;
		w.KeyPress(Key.OemOpenBrackets, RawInputModifiers.Control, PhysicalKey.BracketLeft, "[");
		Assert.Equal(10, w.Tools.Brush.Radius);
	}

	[AvaloniaFact]
	public void TheBracketsAlsoSizeThePlaceBrush()
	{
		var (w, _, _) = Open();
		w.Tools.ChooseMode(ToolMode.Place);
		w.Tools.SizeSlider.Value = 12;
		w.KeyPress(Key.OemCloseBrackets, RawInputModifiers.None, PhysicalKey.BracketRight, "]");
		Assert.Equal(13, w.PlaceTool.Brush.Radius);
	}

	// ---- H.

	[AvaloniaFact]
	public void HChoosesTheViewToolLikeEsc()
	{
		var (w, _, _) = Open();
		w.Tools.ChooseSelect();
		w.KeyPress(Key.H, RawInputModifiers.None, PhysicalKey.H, "h");
		Assert.Equal(ToolMode.View, w.Tools.Mode);
		w.Tools.Choose(BrushTool.Smooth);
		Assert.True(w.Tools.Key("H"));
		Assert.Null(w.Tools.Tool);
		Assert.Equal(ToolMode.View, w.Tools.Mode);
	}

	// ---- Sliding the view.

	[AvaloniaFact]
	public void SpaceAndLeftDragSlideTheViewInsteadOfUsingTheTool()
	{
		var (w, s, screen) = Open(Wall());
		w.Tools.ChooseSelect();
		var before = w.View.Camera;
		w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
		w.MouseDown(screen(40, 40), MouseButton.Left);
		Assert.True(w.View.Panning);
		w.MouseMove(screen(60, 70));
		w.MouseUp(screen(60, 70), MouseButton.Left);
		Assert.False(w.View.Panning);
		Assert.NotEqual(before.Target, w.View.Camera.Target);
		Assert.Equal(before.Yaw, w.View.Camera.Yaw);
		// The Select tool did not take the click (the wall under it stays unselected).
		Assert.Empty(w.View.Selected);
		// Let go of Space: the left button is the tool's again.
		w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
		w.MouseDown(screen(40, 40), MouseButton.Left);
		Assert.False(w.View.Panning);
		w.MouseUp(screen(40, 40), MouseButton.Left);
	}

	[AvaloniaFact]
	public void SpaceDragsInTheBrushToolDoNotPaint()
	{
		var (w, s, screen) = Open();
		w.Tools.Choose(BrushTool.Raise);
		w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
		w.MouseDown(screen(64, 64), MouseButton.Left);
		Assert.False(s.Stroking);
		w.MouseUp(screen(64, 64), MouseButton.Left);
		w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
		Assert.False(s.CanUndo);
	}

	[AvaloniaFact]
	public void ShiftAndRightDragSlideTheViewInsteadOfTurningIt()
	{
		var (w, _, screen) = Open();
		var before = w.View.Camera;
		w.MouseDown(screen(64, 64), MouseButton.Right, RawInputModifiers.Shift);
		w.MouseMove(screen(90, 64), RawInputModifiers.Shift);
		w.MouseUp(screen(90, 64), MouseButton.Right, RawInputModifiers.Shift);
		Assert.Equal(before.Yaw, w.View.Camera.Yaw);
		Assert.Equal(before.Pitch, w.View.Camera.Pitch);
		Assert.NotEqual(before.Target, w.View.Camera.Target);
		// Without Shift the right button turns.
		w.MouseDown(screen(64, 64), MouseButton.Right);
		w.MouseMove(screen(90, 64));
		w.MouseUp(screen(90, 64), MouseButton.Right);
		Assert.NotEqual(before.Yaw, w.View.Camera.Yaw);
	}

	[AvaloniaFact]
	public void ASpaceDragInTheViewToolPicksNothing()
	{
		var (w, _, screen) = Open(Wall());
		w.Tools.Choose(null);
		w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
		// A click (no movement) with Space held slides, it does not pick.
		w.MouseDown(screen(40, 40), MouseButton.Left);
		w.MouseUp(screen(40, 40), MouseButton.Left);
		Assert.Empty(w.View.Selected);
		w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
	}

	[AvaloniaFact]
	public void SpaceNeverPressesTheFocusedButton()
	{
		var (w, _, _) = Open();
		int clicks = 0;
		w.AllButton.Click += (_, _) => clicks++;
		w.ShowRight(w.ViewPanelCard);
		w.AllButton.Focus();
		w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
		w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
		Assert.Equal(0, clicks);
	}

	[AvaloniaFact]
	public void SpaceStillTypesInTextBoxes()
	{
		var w = new Window { Width = 400, Height = 200 };
		var box = new TextBox();
		w.Content = box;
		w.Show();
		new GlView().Attach(new Border(), w);
		box.Focus();
		// The box gets its Space (the view only takes it from other controls).
		int spaces = 0;
		box.KeyDown += (_, e) => spaces += e.Key == Key.Space ? 1 : 0;
		w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
		w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
		Assert.Equal(1, spaces);
	}

	// ---- Alt + wheel.

	[AvaloniaFact]
	public void AltWheelTurnsTheSelectionLikeTheKeys()
	{
		var (a, sa, screenA) = Open(Wall());
		a.Tools.ChooseSelect();
		a.View.Select(new[] { 0 });
		float distance = a.View.Camera.Distance;
		// Towards you (down) as ".", 1°.
		a.MouseWheel(screenA(64, 64), new Avalonia.Vector(0, -1), RawInputModifiers.Alt);
		a.View.SelectTool.Commit();
		Assert.Equal(distance, a.View.Camera.Distance);
		var (b, sb, _) = Open(Wall());
		b.Tools.ChooseSelect();
		b.View.Select(new[] { 0 });
		b.KeyPress(Key.OemPeriod, RawInputModifiers.None, PhysicalKey.Period, ".");
		b.View.SelectTool.Commit();
		Assert.Equal(sb.Scene.Things[^1].Rotation, sa.Scene.Things[^1].Rotation);
		Assert.NotEqual(Vector3.Zero, sa.Scene.Things[^1].Rotation);
	}

	[AvaloniaFact]
	public void AltWheelAwayAndWithShiftTurnTheOtherWayAndFurther()
	{
		var (a, sa, screen) = Open(Wall());
		a.Tools.ChooseSelect();
		a.View.Select(new[] { 0 });
		a.MouseWheel(screen(64, 64), new Avalonia.Vector(0, 1), RawInputModifiers.Alt | RawInputModifiers.Shift);
		a.View.SelectTool.Commit();
		var (b, sb, _) = Open(Wall());
		b.Tools.ChooseSelect();
		b.View.Select(new[] { 0 });
		b.KeyPress(Key.OemComma, RawInputModifiers.Shift, PhysicalKey.Comma, ",");
		b.View.SelectTool.Commit();
		Assert.Equal(sb.Scene.Things[^1].Rotation, sa.Scene.Things[^1].Rotation);
	}

	[AvaloniaFact]
	public void AltWheelWithNothingToTurnZooms()
	{
		var (w, _, screen) = Open(Wall());
		w.Tools.ChooseSelect();
		float d = w.View.Camera.Distance;
		w.MouseWheel(screen(64, 64), new Avalonia.Vector(0, 1), RawInputModifiers.Alt);
		Assert.True(w.View.Camera.Distance < d);
		// In the View tool too, and the plain wheel always zooms.
		w.Tools.Choose(null);
		d = w.View.Camera.Distance;
		w.MouseWheel(screen(64, 64), new Avalonia.Vector(0, -1), RawInputModifiers.Alt);
		Assert.True(w.View.Camera.Distance > d);
		w.Tools.ChooseSelect();
		w.View.Select(new[] { 0 });
		d = w.View.Camera.Distance;
		w.MouseWheel(screen(64, 64), new Avalonia.Vector(0, 1));
		Assert.True(w.View.Camera.Distance < d);
		Assert.True(w.AltWheel(Key.OemComma, false));
		w.Tools.Choose(BrushTool.Raise);
		Assert.False(w.AltWheel(Key.OemComma, false));
	}

	[AvaloniaFact]
	public void AltWheelTurnsThePlacePreview()
	{
		var (w, _, screen) = Open();
		w.Tools.ChooseMode(ToolMode.Place);
		w.PlaceTool.Chosen.Clear();
		w.PlaceTool.Chosen.Add("Beech1");
		float d = w.View.Camera.Distance;
		w.MouseWheel(screen(64, 64), new Avalonia.Vector(0, -1), RawInputModifiers.Alt);
		Assert.Equal(1, w.PlaceTool.Rotation, 3);
		w.MouseWheel(screen(64, 64), new Avalonia.Vector(0, 1), RawInputModifiers.Alt | RawInputModifiers.Shift);
		Assert.Equal(-14, w.PlaceTool.Rotation, 3);
		Assert.Equal(d, w.View.Camera.Distance);
	}

	[AvaloniaFact]
	public void AltWheelTurnsThePasteAsCommaAndPeriodDo()
	{
		var (w, s, screen) = Open();
		var box = new AreaTool { Soft = 0 };
		box.Points.Add(new Vector2(32, 32));
		box.Points.Add(new Vector2(48, 48));
		w.View.Paste.Clip = CopyData.FromArea(box, s.Ground, s.Scene, Array.Empty<int>(), _ => null);
		w.StartPaste();
		Assert.Equal(ToolMode.Paste, w.Tools.Mode);
		w.MouseWheel(screen(64, 64), new Avalonia.Vector(0, 1), RawInputModifiers.Alt);
		Assert.Equal(1, w.View.Paste.Turn, 3);
		w.KeyPress(Key.OemComma, RawInputModifiers.None, PhysicalKey.Comma, ",");
		Assert.Equal(2, w.View.Paste.Turn, 3);
		w.MouseWheel(screen(64, 64), new Avalonia.Vector(0, -1), RawInputModifiers.Alt | RawInputModifiers.Shift);
		Assert.Equal(347, w.View.Paste.Turn, 3);
		w.KeyPress(Key.OemPeriod, RawInputModifiers.Shift, PhysicalKey.Period, ".");
		Assert.Equal(332, w.View.Paste.Turn, 3);
	}

	// ---- Help and Naturalize.

	[AvaloniaFact]
	public void TheHelpListsEveryKey()
	{
		var w = new MainWindow(load: false);
		var texts = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w.HelpCard).OfType<TextBlock>().Select(t => t.Text).ToList();
		foreach (string k in new[] { "[  ]", "Alt + wheel", "E / M / H", "Middle drag", "Place: R", "PgUp / PgDn / End", "Ctrl+S", "V / L / ?", "Alt + click", "I", "Del" })
		{
			Assert.Contains(k, texts);
		}
		Assert.Contains(texts, t => t?.Contains("Space + left drag, Shift + right drag") == true);
	}

	[AvaloniaFact]
	public void NewPatternGivesNaturalizeAnotherNoise()
	{
		var t = new ToolPanel();
		string? said = null;
		t.Message += m => said = m;
		t.NewPattern(1234);
		Assert.Equal(1234, t.Brush.Noise.Seed);
		Assert.Equal("New natural pattern. The next Naturalize stroke or natural path uses it.", said);
		float a = t.Brush.Noise.Fbm(3.3f, 7.1f);
		t.NewPattern(99);
		Assert.NotEqual(a, t.Brush.Noise.Fbm(3.3f, 7.1f));
		// The same seed, the same pattern.
		t.NewPattern(1234);
		Assert.Equal(a, t.Brush.Noise.Fbm(3.3f, 7.1f));
		// The button: a new random one, shown with the Naturalize tool.
		int before = t.Brush.Noise.Seed;
		t.NewPatternButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Assert.NotEqual(before, t.Brush.Noise.Seed);
		t.Choose(BrushTool.Natural);
		Assert.True(((Control)t.NewPatternButton.Parent!).IsVisible);
		t.Choose(BrushTool.Raise);
		Assert.False(((Control)t.NewPatternButton.Parent!).IsVisible);
	}

	[AvaloniaFact]
	public void TheEditorsNaturalizeUsesTheNewPattern()
	{
		var (w, s, _) = Open();
		w.Tools.NewPattern(77);
		Assert.Equal(77, s.Brush.Noise.Seed);
	}
}
