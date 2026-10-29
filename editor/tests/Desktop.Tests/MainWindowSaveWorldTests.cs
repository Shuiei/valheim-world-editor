using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Save on the map (MainWindow.SaveWorld): what the save left out is said with the rest, applying live whose
/// question about the game's own ground fails is said, nothing sent and the changes still pending, and the map shows
/// what is pending once saved or applied before the outcome is told.
/// </summary>
[Collection("World files")]
public sealed class MainWindowSaveWorldTests
{
    [AvaloniaFact]
    public async Task WhatTheSaveLeftOutIsSaid()
    {
        using var tw = new TempWorld();
        var w = await TestWindows.EditingWorld(tw.Dir);
        var s = w.Session!;
        s.Shape(32, 32, Formula.Compile("2", []), 3, 0, "raise");
        // A kind the world has no object of to copy: it cannot be made.
        s.Commit("Unknown", null, [], [(new NewObject(0, StableHash.Of("no_such_kind"), new Vector3(7, 40, 9), Vector3.Zero, 0), false)]);
        var told = new List<string>();
        w.ConfirmSave = _ => Task.FromResult(true);
        w.Tell = t => { told.Add(t); return Task.CompletedTask; };

        await w.SaveWorld();

        Assert.EndsWith("\n\nNot saved:\n• a new object at 7, 9: unknown kind of object", Assert.Single(told));
        Assert.Equal(0, w.World!.Pending.Zones);
    }

    [AvaloniaFact]
    public async Task ApplyingWhoseQuestionFailsIsSaidAndNothingIsSent()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        r.Raise();
        var world = r.W.World!;
        // The game sends other ground for the zone changed: applying asks first.
        var zone = Assert.Single(world.Edits.Copies(e => e.Changed));
        var other = world.World.TerrainZones.Any(t => (t.ZoneX, t.ZoneZ) == (zone.ZoneX, zone.ZoneZ)) ? null : world.World.TerrainZones[0];
        Assert.Equal(EditStore.GameGround.Kept, world.Edits.TakeGameGround(zone.ZoneX, zone.ZoneZ, other));
        r.W.Ask = (_, _, _, _) => throw new InvalidOperationException("no dialog");

        await r.W.SaveWorld();

        Assert.Equal("Could not apply live: no dialog", r.Told[^1]);
        Assert.Empty(r.Game!.Terrain);
        Assert.Equal(1, world.Pending.Zones);
        Assert.False(r.W.IsBusy);
    }

    // The outcome's dialog over a map that still said the changes were waiting (it was brought up to date only once the
    // dialog closed).
    [AvaloniaFact]
    public async Task TheMapIsUpToDateWhenTheOutcomeIsTold()
    {
        foreach (bool live in new[] { false, true })
        {
            using var r = new WorldWindowRun();
            await (live ? r.OpenLive() : r.Open());
            r.Raise();
            r.W.ShowMap();
            var map = r.W.MapPage!;
            string? behind = null;
            r.W.Tell = _ => { behind = map.Pending.Text; return Task.CompletedTask; };
            await r.W.SaveWorld();
            Assert.Equal(live ? "Everything is applied." : "Everything is saved.", behind);
            map.Stop();
        }
    }
}
