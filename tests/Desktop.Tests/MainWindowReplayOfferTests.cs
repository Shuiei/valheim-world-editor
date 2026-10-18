using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The offer to bring the pending changes onto a save the game made since (MainWindow.OfferReplay): declined, the
/// changes stay pending and the status line says so; accepted, they go onto the game's save, each kind of clash asked
/// about, and the area open is read again; without a newer save there is nothing to bring them onto.
/// </summary>
[Collection("World files")]
public sealed class MainWindowReplayOfferTests
{
    /// <summary>
    /// A window editing zone 0, 0 of the world, the ground raised there, then the game saves the world (an object of
    /// its own removed): the editor's save would be refused.
    /// </summary>
    /// <returns>The window, and the number of the game's save.</returns>
    private static async Task<(MainWindow W, int Game)> ChangedUnderTheGame(string dir)
    {
        var w = await TestWindows.EditingWorld(dir);
        var s = w.Session!;
        s.Shape(32, 32, Formula.Compile("2", []), 3, 0, "raise");
        var world = w.World!;
        int other = world.World.Objects.First(o => MathF.Abs(o.Position.X) < 200 && MathF.Abs(o.Position.Z) < 200).Id;
        Assert.True(WorldWriter.Save(WorldSave.Load(dir), [], [other], options: new WorldWriter.Options(KeepBase: true)).Saved);
        int game = world.World.SaveNumber + 1;
        Assert.Equal(game, world.GameSaveSince());
        return (w, game);
    }

    [AvaloniaFact]
    public async Task DeclinedTheChangesStayPendingAndTheStatusLineSaysSo()
    {
        using var tw = new TempWorld();
        var (w, game) = await ChangedUnderTheGame(tw.Dir);
        var asked = new List<string>();
        w.Ask = (title, _, _, _) => { asked.Add(title); return Task.FromResult(false); };
        w.Tell = _ => Task.CompletedTask;

        await w.OfferReplay(w.World!, game, w.View.Scene);

        Assert.Single(asked);
        Assert.Contains(game.ToString(System.Globalization.CultureInfo.InvariantCulture), w.MessageText.Text);
        Assert.Equal(game, w.World!.GameSaveSince());
        Assert.Equal(1, w.World.Pending.Zones);
    }

    [AvaloniaFact]
    public async Task AcceptedTheChangesGoOntoTheGamesSaveAndTheAreaIsReadAgain()
    {
        using var tw = new TempWorld();
        var (w, game) = await ChangedUnderTheGame(tw.Dir);
        var before = w.View.Scene;
        var told = new List<string>();
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        w.Tell = t => { told.Add(t); return Task.CompletedTask; };

        await w.OfferReplay(w.World!, game, before);

        Assert.StartsWith($"Your changes are on the game's save #{game} now", Assert.Single(told));
        Assert.Null(w.World!.GameSaveSince());
        Assert.Equal(game, w.World.World.SaveNumber);
        Assert.Equal(1, w.World.Pending.Zones);
        // The area was read again: a new scene, with the raised ground still pending on it.
        Assert.NotSame(before, w.View.Scene);
        Assert.NotNull(w.Session);
    }

    // The game removed an object the editor moved: the clash is asked about, and left to the game here.
    [AvaloniaFact]
    public async Task EachKindOfClashIsAskedAbout()
    {
        using var tw = new TempWorld();
        var w = await TestWindows.EditingWorld(tw.Dir);
        TestScenes.MovedThenRemovedByTheGame(w.Session!, tw.Dir);
        int game = w.World!.GameSaveSince()!.Value;
        var asked = new List<string>();
        w.Ask = (title, text, _, _) => { asked.Add(title + "|" + text); return Task.FromResult(asked.Count == 1); };
        w.Tell = _ => Task.CompletedTask;

        await w.OfferReplay(w.World!, game, w.View.Scene);

        Assert.Equal(2, asked.Count);
        Assert.Contains("removed", asked[1], StringComparison.OrdinalIgnoreCase);
        Assert.Null(w.World!.GameSaveSince());
    }

    // No newer save of the game's after all (it was put back, or the number read wrong): nothing to bring the changes onto.
    [AvaloniaFact]
    public async Task WithoutANewerSaveTheOfferSaysItFailed()
    {
        using var tw = new TempWorld();
        var w = await TestWindows.EditingWorld(tw.Dir);
        var told = new List<string>();
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        w.Tell = t => { told.Add(t); return Task.CompletedTask; };

        await w.OfferReplay(w.World!, w.World!.World.SaveNumber + 1, w.View.Scene);

        Assert.Contains("the game's save is not there any more", Assert.Single(told));
    }
}
