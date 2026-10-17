using TerrainEditor.Editing;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Apply live's outcomes in another language (the made-up one: PseudoLang): all of it applied (the live sync's object
/// summary in it), part of it before the game failed, and nothing when the game refused the ground are each shown in the
/// language in use, with their English kept for Claude.
/// </summary>
[Collection("DataDir")]
public sealed class WorldSessionLangTests
{
    private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);

    /// <summary>The outcome's message is shown in the language in use; its English starts as given, with nothing translated.</summary>
    private static void Shown(WorldSession.Outcome o, string english)
    {
        Assert.StartsWith(PseudoLang.Open.ToString(), o.Message);
        Assert.True(PseudoLang.Translated(o.Message, []), o.Message);
        Assert.Matches(english, o.Report.English);
        Assert.DoesNotContain(PseudoLang.Open, o.Report.English);
    }

    /// <summary>A live world with a raise and an object deleted, waiting to be applied.</summary>
    private static async Task<(WorldSession World, EditSession Area)> Changed(FakeGame game)
    {
        var (world, s) = await TestScenes.LiveArea(game);
        s.Shape(10, 10, Two, 3, 0, "raise");
        s.Delete([s.Scene.Things.FindIndex(t => t.Id >= 0 && !t.Gone)]);
        return (world, s);
    }

    [Fact]
    public async Task ApplyLiveSaysWhatWentInInTheLanguageAndInEnglish()
    {
        using var pseudo = new PseudoLang();
        using (var game = new FakeGame())
        {
            var (world, s) = await Changed(game);
            using (world)
            {
                Shown(await s.ApplyLive(), @"^Applied to the running game: 1 zone\(s\) of ground; \d+ object\(s\) removed, 0 created\.$");
            }
        }
        using (var game = new FakeGame())
        {
            var (world, s) = await Changed(game);
            using (world)
            {
                game.Fail["/objects"] = 500;
                Shown(await s.ApplyLive(), @"^Applied to the running game: 1 zone\(s\) of ground; but then it failed \(.+\): Apply live again for the rest\.$");
            }
        }
        using (var game = new FakeGame())
        {
            var (world, s) = await Changed(game);
            using (world)
            {
                game.Fail["/terrain"] = 400;
                Shown(await s.ApplyLive(), "^Could not apply live: ");
            }
        }
    }

    // Apply live with nothing to send, declined over ground the game changed too, and zone resets after which the world
    // could not be read again from the game: each said in the language in use, with its English kept.
    [Fact]
    public async Task TheOtherApplyLiveOutcomesAreInTheLanguageAndInEnglish()
    {
        using var pseudo = new PseudoLang();
        using var game = new FakeGame();
        var (world, s) = await TestScenes.LiveArea(game);
        Shown(await world.ApplyLive(), @"^Nothing to apply\.$");

        s.Shape(10, 10, Two, 3, 0, "raise");
        // The game sends other ground for the zone changed: the change waits over it.
        var zone = Assert.Single(world.Edits.Copies(e => e.Changed));
        var other = world.World.TerrainZones.Any(t => (t.ZoneX, t.ZoneZ) == (zone.ZoneX, zone.ZoneZ)) ? null : world.World.TerrainZones[0];
        Assert.Equal(EditStore.GameGround.Kept, world.Edits.TakeGameGround(zone.ZoneX, zone.ZoneZ, other));
        Shown(await world.ApplyLive(_ => Task.FromResult(false)), "^Not applied: the game changed the ground where you have changes ");

        world.Edits.SetReset(new ZoneReset(5, 5, true, false), true);
        game.Fail["/snapshot"] = 500;
        Shown(await world.ApplyLive(), @"^Applied to the running game: .*1 zone\(s\) reset; but the world could not be read again from the game \(.+\): ");
    }

    // Changes brought onto a save the game made since: refused when the game saved again meanwhile; then what was
    // brought over, the game's version kept for an object it removed, and the steps dropped with it. Each said in the
    // language in use, with its English kept.
    [Fact]
    public void BringingTheChangesOverSaysWhatItDidInTheLanguageAndInEnglish()
    {
        using var pseudo = new PseudoLang();
        using var w = new TempWorld();
        using var world = WorldSession.Open(w.Dir);
        var s = WorldScene.Load(world, 0, 0, 1).Session!;
        s.Shape(32, 32, Two, 3, 0, "raise");
        TestScenes.MovedThenRemovedByTheGame(s, w.Dir);
        var plan = world.PrepareReplay()!;
        // The game saves again while its save is read.
        int n = plan.Newer.SaveNumber;
        foreach (string ext in new[] { ".chunks", ".db2", ".fwl2", ".ok" })
        {
            File.Copy(Path.Combine(w.Dir, $"_main.{n}{ext}"), Path.Combine(w.Dir, $"_main.{n + 1}{ext}"));
        }
        Shown(world.Replay(plan, _ => false), @"^Not brought over: the game saved this world again ");

        plan = world.PrepareReplay()!;
        Shown(world.Replay(plan, _ => false),
            @"^Your changes are on the game's save #\d+ now, .* The game's version stays for 0 zone\(s\) and 1 object\(s\)\. History: 0 step\(s\) kept, 2 dropped ");
    }
}
