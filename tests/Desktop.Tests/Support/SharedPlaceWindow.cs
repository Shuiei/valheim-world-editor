using System.Numerics;
using Avalonia.Threading;
using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The 1600 × 1000 window the Place tests drive: a shown main window, a flat 2 × 2 zone area or the test world, and the
/// camera looking straight down, shared by the Place panel, tool and input tests.
/// </summary>
internal static class SharedPlaceWindow
{
    /// <summary>The size of the window and of the screen the Place tests click on.</summary>
    internal static readonly Avalonia.Size Size = new(1600, 1000);

    /// <summary>
    /// A shown main window of the Place tests' size, with nothing edited yet, remembering in the given prefs.
    /// </summary>
    internal static MainWindow Create(Prefs? prefs = null)
    {
        var w = new MainWindow(load: false, prefs: prefs) { Width = Size.Width, Height = Size.Height };
        w.Show();
        return w;
    }

    /// <summary>
    /// A shown main window editing a flat 2 × 2 zone area with these objects, in the Place tool, looking straight down.
    /// </summary>
    internal static (MainWindow W, EditSession S) Open(params WorldScene.Thing[] things)
    {
        var w = Create();
        var s = TestScenes.Flat(2, things);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseMode(ToolMode.Place);
        w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1.6f);
        return (w, s);
    }

    /// <summary>
    /// Chooses Beech1 as the Place tool's only kind, as the panel does, and sets the remembered kinds to match.
    /// </summary>
    internal static void ChooseBeech(MainWindow w)
    {
        w.PlaceTool.Chosen.Clear();
        w.PlaceTool.Chosen.Add("Beech1");
        // The window's Place panel took the kinds remembered by earlier tests (pieces from the Workshop turn
        // One at a time and End to end on): set again for the kinds chosen here, as choosing them in the panel does.
        w.PlaceTool.AutoSnap();
    }

    /// <summary>
    /// Chooses these kinds in the Place panel of the run, as ticking them in the panel does.
    /// </summary>
    internal static void ChoosePanelKinds(PanelRun r, params string[] names)
    {
        r.T.Chosen.Clear();
        r.T.Chosen.AddRange(names);
        // What ticking a kind does in the panel.
        typeof(PlacePanel).GetMethod("Chosen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(r.P, [true]);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// A drag with the input along these grid points of the window's view: pressed on the first, moved over the rest and
    /// released on the last.
    /// </summary>
    internal static void Drag(PlaceInput input, MainWindow w, Avalonia.Size size, params (float X, float Z)[] path)
    {
        input.Down(At(w, path[0].X, path[0].Z), size, false, false, false, 1);
        foreach (var (x, z) in path.Skip(1))
        {
            input.Moved(At(w, x, z), size);
        }
        input.Up(At(w, path[^1].X, path[^1].Z), size);
    }

    /// <summary>
    /// The screen point of grid point (gx, gz) in the window's view (the ground is at 30 m).
    /// </summary>
    internal static Avalonia.Point At(MainWindow w, float gx, float gz)
    {
        var q = Vector4.Transform(new Vector4(gx - 64, 30, -(gz - 64), 1), w.View.ViewProj);
        return new Avalonia.Point((q.X / q.W + 1) / 2 * Size.Width, (1 - q.Y / q.W) / 2 * Size.Height);
    }

    /// <summary>
    /// A main window on its own copy of the test world (zone 0, 0) in the Place tool, with the panel's memory emptied;
    /// deleted when disposed.
    /// </summary>
    internal sealed class PanelRun : IDisposable
    {
        /// <summary>The window, shown at the Place tests' size.</summary>
        public MainWindow W { get; }

        /// <summary>The folder holding the copy of the test world.</summary>
        public string Dir { get; } = Fixtures.CopyFixture();

        /// <summary>The copy of the test world, zone 0, 0.</summary>
        public WorldScene Scene { get; }

        /// <summary>The Place panel of the window.</summary>
        public PlacePanel P => W.PlacePanel;

        /// <summary>The Place tool of the window.</summary>
        public PlaceTool T => W.PlaceTool;

        /// <summary>The last message the panel said, or null.</summary>
        public string? Said;

        /// <summary>The area's edit session.</summary>
        public EditSession S => Scene.Session!;

        /// <summary>
        /// Opens the copy of the test world in the given tool, with the panel's memory emptied.
        /// </summary>
        public PanelRun(ToolMode mode = ToolMode.Place, Prefs? prefs = null)
        {
            W = Create(prefs);
            Scene = WorldScene.Load(Dir, 0, 0, 1);
            W.View.Show(Scene, null);
            W.Edit(Scene.Session!);
            W.Tools.ChooseMode(mode);
            P.Message += t => Said = t.Shown;
            // A clean memory of its own (the tests' place.json is shared).
            P.Memory.Presets.Clear();
            P.Memory.Favourites.Clear();
            P.Memory.Recent.Clear();
        }

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
    }
}
