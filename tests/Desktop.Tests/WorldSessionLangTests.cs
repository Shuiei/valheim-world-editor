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
}
