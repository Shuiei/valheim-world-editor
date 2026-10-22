using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Scripts run from the window (RunScriptCode, as the Script tool and Claude run them): with no area, a dry run that
/// says all it would change, a second script while one runs, a run whose area changed or was left meanwhile, and the
/// limit that stopped points.
/// </summary>
[Collection("World files")]
public class MainWindowScriptTests
{
    private const string Raise = "Ground.Raise(32, 32, 1);";

    /// <summary>
    /// A shown window editing a flat 2 × 2 zone area (ground at 30 m) with a beech at its middle.
    /// </summary>
    private static (MainWindow W, EditSession S) Open()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2, new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(32, 30, 32), Vector3.Zero, 1, false));
        w.View.Show(s.Scene, null);
        w.Edit(s);
        return (w, s);
    }

    [AvaloniaFact]
    public async Task WithoutAnAreaNothingRuns()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var r = await w.RunScriptCode(Raise, "Script: test");
        Assert.False(r.Changed);
        Assert.Equal("Open an area first: a script works on the open area.", r.Message.English);
    }

    [AvaloniaFact]
    public async Task ADryRunSaysAllItWouldChangeAndChangesNothing()
    {
        var (w, s) = Open();
        int steps = s.UndoList.Count;
        var r = await w.RunScriptCode("""
            Ground.Raise(32, 32, 1);
            Ground.Paint(32, 32, "paved");
            foreach (var o in Objects.All)
                Objects.Remove(o);
            Objects.Place("rock4_forest", 20, 20);
            """, "Script: test", apply: false);
        Assert.False(r.Changed);
        Assert.StartsWith("Dry run: the script would change ", r.Message.English);
        Assert.Contains("ground point(s)", r.Message.English);
        Assert.Contains("painted", r.Message.English);
        Assert.Contains("object(s) placed", r.Message.English);
        Assert.Contains("1 taken away", r.Message.English);
        Assert.EndsWith("; nothing changed.", r.Message.English);
        Assert.Equal(steps, s.UndoList.Count);
        Assert.False(s.Scene.Things[0].Gone);
    }

    // Moves are counted in what a script changed, except those of objects it also took away.
    [AvaloniaFact]
    public async Task AScriptThatOnlyMovesSaysHowManyMoved()
    {
        var (w, _) = Open();
        var r = await w.RunScriptCode("""
            foreach (var o in Objects.All)
                Objects.Move(o, y: o.Y + 1);
            """, "Script: test");
        Assert.True(r.Changed);
        Assert.StartsWith("The script changed 1 moved in ", r.Message.English);

        var gone = await w.RunScriptCode("""
            foreach (var o in Objects.All)
            {
                Objects.Move(o, y: o.Y + 1);
                Objects.Remove(o);
            }
            """, "Script: test", apply: false);
        Assert.Equal("Dry run: the script would change 1 taken away; nothing changed.", gone.Message.English);
    }

    [AvaloniaFact]
    public async Task OneScriptAtATimeAndNothingChangesWhenTheAreaChangedMeanwhile()
    {
        var (w, s) = Open();
        var first = w.RunScriptCode(Raise, "Script: first");
        // Another while it runs: refused, from the panel too.
        var second = await w.RunScriptCode(Raise, "Script: second");
        Assert.Equal("A script is already running: nothing changed.", second.Message.English);
        w.MessageText.Text = "";
        await w.RunScript();
        Assert.Equal("", w.MessageText.Text);
        // The area changes before the first one is done.
        s.Shape(20, 20, Formula.Compile("2", []), 3, 0, "raise");
        int steps = s.UndoList.Count;
        var done = await first;
        Assert.False(done.Changed);
        Assert.Equal("The area changed while the script ran: nothing changed. Run it again.", done.Message.English);
        Assert.Equal(steps, s.UndoList.Count);
    }

    [AvaloniaFact]
    public async Task NothingChangesWhenAnotherAreaWasOpenedMeanwhile()
    {
        var (w, s) = Open();
        var first = w.RunScriptCode(Raise, "Script: first");
        var other = TestScenes.Flat(1);
        w.View.Show(other.Scene, null);
        w.Edit(other);
        var done = await first;
        Assert.False(done.Changed);
        Assert.Equal("Another area was opened while the script ran: nothing changed.", done.Message.English);
        Assert.Empty(s.UndoList);
        Assert.Empty(other.UndoList);
    }

    // The points a limit stopped are said with the limit that stopped them: the game's ±8 m, or No limit's ±400 m.
    [Fact]
    public void TheLimitThatStoppedPointsIsTheOneSaid()
    {
        Assert.Equal("", MainWindow.Clamped(0, true).English);
        Assert.Equal("3 point(s) stopped at the game's ±8 m (Ground.NoLimit = true lets them go further).", MainWindow.Clamped(3, false).English);
        Assert.Equal("3 point(s) stopped at No limit's ±400 m.", MainWindow.Clamped(3, true).English);
    }
}
