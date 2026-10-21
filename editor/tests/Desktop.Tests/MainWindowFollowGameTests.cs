using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using Xunit;
using static TerrainEditor.Desktop.Tests.LiveFollowSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Live, the window following the game in the zones shown: what came (new objects, ground) said in the status bar, the
/// game's ground taken where the editor's changes were undone, a plugin too old to follow (said once, then not asked
/// again), and what was read dropped when another area opened meanwhile.
/// </summary>
[Collection("World files")]
public class MainWindowFollowGameTests
{
    [AvaloniaFact]
    public async Task NewObjectsAndGroundAreSaidAndTheGamesGroundComesBackWhereChangesWereUndone()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        var game = r.Game!;
        var wsave = r.W.World!.World;
        var s = r.W.Session!;
        int tree = s.Scene.Things.FindIndex(t => !t.Gone && !t.Piece && t.Id >= 0 && wsave.ObjectRefs[t.Id].Zone == (0, 0));
        byte[] bytes = wsave.ObjectBytes(s.Scene.Things[tree].Id);
        // The game dug zone 0, 0 and has an object more there.
        var dug = new EditStore(wsave).Get(0, 0)!;
        dug.Modified[2000] = true;
        dug.Level[2000] = -3;
        var objects = Dug(wsave, dug);
        objects.Add((99, 1, bytes));
        game.Zones[(0, 0)] = (1, objects);
        await r.W.FollowGame();
        await TestWindows.Until(() => r.Said.StartsWith("From the game:", StringComparison.Ordinal));
        Assert.Equal("From the game: 1 new object(s), the ground of 1 zone(s).", r.Said);
        // The editor raises that zone while the game digs it again: the editor's ground is kept.
        r.Raise(10, 10);
        dug.Level[2000] = -5;
        game.Zones[(0, 0)] = (2, Dug(wsave, dug));
        await r.W.FollowGame();
        await TestWindows.Until(() => r.Said.Contains("where you have changes not applied", StringComparison.Ordinal));
        // Undone: the game's ground there.
        await r.W.Undo();
        await r.W.FollowGame();
        await TestWindows.Until(() => r.Said == "From the game: the ground of 1 zone(s), where your changes were undone.");
    }

    [AvaloniaFact]
    public async Task APluginTooOldToFollowIsSaidOnceAndNotAskedAgain()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        r.Game!.Fail["/watch"] = 404;
        await r.W.FollowGame();
        await TestWindows.Until(() => r.Said.StartsWith("The WorldEditorBridge plugin on the server is older than this editor", StringComparison.Ordinal));
        // Not asked again for this world.
        r.Game.Fail.Clear();
        r.W.MessageText.Text = "";
        await r.W.FollowGame();
        Assert.Equal("", r.Said);
    }

    [AvaloniaFact]
    public async Task WhatWasReadIsDroppedWhenAnotherAreaOpenedMeanwhile()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        var wsave = r.W.World!.World;
        r.Game!.Zones[(0, 0)] = (1, AsRead(wsave, (0, 0)));
        var following = r.W.FollowGame();
        // An area of no world opened while the game answers.
        var other = TestScenes.Flat(1);
        r.W.View.Show(other.Scene, null);
        r.W.Edit(other);
        await following;
        Assert.Same(other, r.W.Session);
        r.W.MessageText.Text = "";
        await r.W.FollowGame();
        Assert.Equal("", r.Said);
    }
}
