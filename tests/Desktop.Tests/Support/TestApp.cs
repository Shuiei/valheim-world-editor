using Avalonia;
using Avalonia.Headless;

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
    /// Before any test runs (headless or not): the app's files are never the user's (Core's are kept by
    /// <see cref="WorldEditor.Tests.TestFiles"/>).
    /// </summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void KeepTheUsersFiles()
    {
        // The Place tool's memory goes to a file of the tests', not the user's.
        PlaceMemory.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-place-{Environment.ProcessId}.json");
        Stamps.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-stamps-{Environment.ProcessId}.json");
        // What the editor remembers between runs, and saved selections: files of the tests' too.
        Prefs.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-prefs-{Environment.ProcessId}.json");
        SavedSelections.PathOverride ??= Path.Combine(Path.GetTempPath(), $"vwe-selections-{Environment.ProcessId}.json");
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
