using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What the live sync says Apply live did with the objects, in another language (the made-up one: PseudoLang): objects
/// moved in place or made again at their new place, made again by another mod, already gone, and skipped are each a
/// catalogue text, with the English kept for Claude.
/// </summary>
[Collection("DataDir")]
public sealed class LiveSyncLangTests
{
    /// <summary>The summary is shown in the language in use, every part of it; its English matches the pattern given.</summary>
    private static void Shown(Said said, string english)
    {
        Assert.StartsWith(PseudoLang.Open.ToString(), said.Shown);
        Assert.True(PseudoLang.Translated(said.Shown, []), said.Shown);
        Assert.Matches(english, said.English);
        Assert.DoesNotContain(PseudoLang.Open, said.English);
    }

    /// <summary>
    /// A tree moved live as the editor moves things (deleted, with a copy keeping its data at the new place), to a plugin
    /// that moves it in place or makes it again there, and finds this many objects another mod made again.
    /// </summary>
    private static async Task<Said> MovedLive(bool inPlace, int refound)
    {
        using var bridge = new FakeBridge { MoveInPlace = inPlace, Refound = refound };
        bridge.Features.Add("move");
        using var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);
        var (id, prefab, position, rotation, _) = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
        edits.SetDeleted([id], true);
        edits.AddObjects([new NewObject(-1, prefab, position + new Vector3(4, 0, 2), rotation + new Vector3(0, 30, 0), 0f, id, Fresh: false)]);
        return await new LiveSync().Apply(world, edits, live);
    }

    [Fact]
    public async Task MovesAndObjectsMadeAgainAreSaidInTheLanguageAndInEnglish()
    {
        using var pseudo = new PseudoLang();
        Shown(await MovedLive(inPlace: true, refound: 1), @", 1 moved \(1 made again by another mod, found at their place\)$");
        Shown(await MovedLive(inPlace: false, refound: 0), @", 0 moved \(1 more made again at their new place: a player near them would not see them move\)$");
    }

    [Fact]
    public async Task ObjectsAlreadyGoneAndSkippedAreSaidInTheLanguageAndInEnglish()
    {
        using var pseudo = new PseudoLang();
        using var game = new FakeGame { Missing = 1 };
        using var live = new LiveBridge(game.Url, game.Token);
        var world = await live.LoadWorld();
        var edits = new EditStore(world);
        edits.SetDeleted([world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1")).Id], true);
        edits.AddObjects([new NewObject(-1, StableHash.Of("NoSuchThing_xyz"), new Vector3(7, 30, 9), Vector3.Zero, 1f)]);
        Shown(await new LiveSync().Apply(world, edits, live), @"^0 object\(s\) removed, 0 created, 1 were already gone, 1 skipped \(unknown kind of object at 7, 9\)$");
    }
}
