using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TerrainEditor.Desktop.Tests.TestApp))]
// One Avalonia app for the whole run (each test closes its own windows: CloseWindowsAfterEachTest).
// A new app per test was set up now and then on another thread than the dispatcher's, which failed
// the test's cleanup ("The calling thread cannot access this object") at random, on CI too.
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tests' Avalonia app (headless), and the files the editor keeps pointed at the tests' own.
/// </summary>
public static class TestApp
{
    /// <summary>
    /// Before any test runs (headless or not): the user's files are never the tests'.
    /// </summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void KeepTheUsersFiles()
    {
        // The Place tool's memory goes to a file of the tests', not the user's.
        PlaceMemory.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-place-{Environment.ProcessId}.json");
        Stamps.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-stamps-{Environment.ProcessId}.json");
        TerrainEditor.App.AppSettings.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-settings-{Environment.ProcessId}.json");
        TerrainEditor.App.ServerConfig.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-servers-{Environment.ProcessId}.cfg");
        // What the editor remembers between runs, and saved selections: files of the tests' too.
        Prefs.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-prefs-{Environment.ProcessId}.json");
        SavedSelections.PathOverride ??= Path.Combine(Path.GetTempPath(), $"vwe-selections-{Environment.ProcessId}.json");
        // "My game" looks only where a test says, never in the player's own Valheim or profiles.
        TerrainEditor.App.LocalGame.SearchDefaultPlaces = false;
        // Never the game's own Homestead folder (a test run once left blueprints in it).
        TerrainEditor.App.Homestead.FolderOverride ??= Path.Combine(Path.GetTempPath(), $"vwe-homestead-{Environment.ProcessId}");
        // Nor Steam outside the home (the computer's own Valheim, its plugin's settings).
        TerrainEditor.App.GameLook.SearchOutsideHome = false;
        // Blueprints, heightmaps, logs: a data folder of the tests', never the user's.
        TerrainEditor.App.AppSettings.DataDirOverride = Path.Combine(Path.GetTempPath(), $"vwe-data-{Environment.ProcessId}");
        // A changed game folder never starts a copy of the game's look.
        SettingsDialog.CheckGameLook = _ => { };
    }

    /// <summary>
    /// The headless app the Avalonia tests run in, with windows closed after each test.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        CloseWindowsAfterEachTestAttribute.Hook();
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}

/// <summary>
/// The mouse and keys reach the 3D view's camera through the window (the OpenGL picture itself cannot be hit by the
/// pointer: a transparent surface over it takes the input).
/// </summary>
public class InputTests
{
    /// <summary>
    /// An 800 × 600 window shown, and its 3D view.
    /// </summary>
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
        var (yaw, pitch, _, target) = v.Camera;
        w.MouseDown(new Point(400, 300), MouseButton.Right);
        w.MouseMove(new Point(500, 340));
        w.MouseUp(new Point(500, 340), MouseButton.Right);
        var (afterYaw, afterPitch, _, afterTarget) = v.Camera;
        Assert.Equal(yaw - 100 * 0.005f, afterYaw, 3);
        Assert.Equal(pitch + 40 * 0.005f, afterPitch, 3);
        Assert.Equal(target, afterTarget);
    }

    [AvaloniaFact]
    public void MiddleDragSlidesTheCamera()
    {
        var (w, v) = Open();
        var (yaw, _, _, target) = v.Camera;
        w.MouseDown(new Point(400, 300), MouseButton.Middle);
        w.MouseMove(new Point(460, 300));
        w.MouseUp(new Point(460, 300), MouseButton.Middle);
        Assert.NotEqual(target, v.Camera.Target);
        Assert.Equal(yaw, v.Camera.Yaw);
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
        var (yaw, _, _, _) = v.Camera;
        w.MouseDown(new Point(30, 20), MouseButton.Right);
        w.MouseMove(new Point(130, 20));
        w.MouseUp(new Point(130, 20), MouseButton.Right);
        Assert.Equal(yaw, v.Camera.Yaw);
    }
}
