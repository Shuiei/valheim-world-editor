using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TerrainEditor.Desktop.Tests.TestApp))]

namespace TerrainEditor.Desktop.Tests;

public static class TestApp
{
	// Before any test runs (headless or not): the user's files are never the tests'.
	[System.Runtime.CompilerServices.ModuleInitializer]
	internal static void KeepTheUsersFiles()
	{
		// The Place tool's memory goes to a file of the tests', not the user's.
		PlaceMemory.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-place-{Environment.ProcessId}.json");
		Stamps.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-stamps-{Environment.ProcessId}.json");
		TerrainEditor.App.AppSettings.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-settings-{Environment.ProcessId}.json");
		TerrainEditor.App.ServerConfig.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-servers-{Environment.ProcessId}.cfg");
		// "My game" looks only where a test says, never in the player's own Valheim or profiles.
		TerrainEditor.App.LocalGame.SearchDefaultPlaces = false;
		// Blueprints, heightmaps, logs: a data folder of the tests', never the user's.
		TerrainEditor.App.AppSettings.DataDirOverride = Path.Combine(Path.GetTempPath(), $"vwe-data-{Environment.ProcessId}");
		// A changed game folder never starts a copy of the game's look.
		SettingsDialog.CheckGameLook = _ => { };
	}

	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
	}
}

// The mouse and keys reach the 3D view's camera through the window (the OpenGL picture itself cannot
// be hit by the pointer: a transparent surface over it takes the input).
public class InputTests
{
	private static (MainWindow Window, GlView View) Open()
	{
		var w = new MainWindow(load: false) { Width = 800, Height = 600 };
		w.Show();
		return (w, w.View);
	}

	[AvaloniaFact]
	public void RightDragTurnsTheCamera()
	{
		var (w, v) = Open();
		var before = v.Camera;
		w.MouseDown(new Point(400, 300), MouseButton.Right);
		w.MouseMove(new Point(500, 340));
		w.MouseUp(new Point(500, 340), MouseButton.Right);
		var after = v.Camera;
		Assert.Equal(before.Yaw - 100 * 0.005f, after.Yaw, 3);
		Assert.Equal(before.Pitch + 40 * 0.005f, after.Pitch, 3);
		Assert.Equal(before.Target, after.Target);
	}

	[AvaloniaFact]
	public void MiddleDragSlidesTheCamera()
	{
		var (w, v) = Open();
		var before = v.Camera;
		w.MouseDown(new Point(400, 300), MouseButton.Middle);
		w.MouseMove(new Point(460, 300));
		w.MouseUp(new Point(460, 300), MouseButton.Middle);
		Assert.NotEqual(before.Target, v.Camera.Target);
		Assert.Equal(before.Yaw, v.Camera.Yaw);
	}

	[AvaloniaFact]
	public void WheelZooms()
	{
		var (w, v) = Open();
		float d = v.Camera.Distance;
		w.MouseWheel(new Point(400, 300), new Vector(0, 2));
		Assert.Equal(d * 0.88f * 0.88f, v.Camera.Distance, 2);
		w.MouseWheel(new Point(400, 300), new Vector(0, -2));
		Assert.Equal(d, v.Camera.Distance, 2);
	}

	[AvaloniaFact]
	public void KeysAreHeldWhileDown()
	{
		var (w, v) = Open();
		w.KeyPress(Key.W, RawInputModifiers.None, PhysicalKey.W, "w");
		Assert.Contains(Key.W, v.KeysHeld);
		w.KeyRelease(Key.W, RawInputModifiers.None, PhysicalKey.W, "w");
		Assert.DoesNotContain(Key.W, v.KeysHeld);
	}

	[AvaloniaFact]
	public void TheInfoPanelDoesNotTakeTheViewsInput()
	{
		// A drag that starts on the panel (top left) leaves the camera alone.
		var (w, v) = Open();
		var before = v.Camera;
		w.MouseDown(new Point(30, 20), MouseButton.Right);
		w.MouseMove(new Point(130, 20));
		w.MouseUp(new Point(130, 20), MouseButton.Right);
		Assert.Equal(before.Yaw, v.Camera.Yaw);
	}
}
