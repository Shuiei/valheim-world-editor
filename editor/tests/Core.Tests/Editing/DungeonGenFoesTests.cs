using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Generated dungeons (DungeonGen) and what they hold: the foes and boss the settings choose take the biome's place, no respawn
/// or bone pile raises the dead, fewer lights and less rubble come with the settings, and a small crypt says when it has no room for its arena.
/// </summary>
public class DungeonGenFoesTests
{
    /// <summary>
    /// A two-level Swamp crypt (its boss: a Draugr elite), with settings changed.
    /// </summary>
    private static DungeonGen.Result Crypt(Func<DungeonGen.Settings, DungeonGen.Settings>? change = null) =>
        DungeonGen.Make((change ?? (s => s))(new DungeonGen.Settings(Biome: "Swamp", Style: "Crypt", Size: 2, Levels: 2, Seed: 4)));

    /// <summary>
    /// The distinct prefabs of a dungeon's objects that pass the test.
    /// </summary>
    private static HashSet<string> Prefabs(DungeonGen.Result r, Func<string, bool> which) => [.. r.Items.Select(i => i.Prefab).Where(which)];

    [Fact]
    public void ChosenFoesAndBossTakeTheBiomesPlace()
    {
        static bool Spawner(string p) => p.StartsWith("Spawner_", StringComparison.Ordinal);
        Assert.Contains("Spawner_Blob", Prefabs(Crypt(), Spawner));
        // Foes by creature or spawner name; one of no biome is taken as it is.
        var foes = Prefabs(Crypt(s => s with { Foes = ["Draugr", "Spawner_MadeUp"] }), Spawner);
        Assert.Contains("Spawner_Draugr", foes);
        Assert.Contains("Spawner_MadeUp", foes);
        Assert.DoesNotContain("Spawner_Blob", foes);
        // The boss: the biome's, another, or none.
        Assert.Contains("Draugr_Elite", Prefabs(Crypt(), p => !Spawner(p)));
        var troll = Prefabs(Crypt(s => s with { Boss = "Troll" }), p => !Spawner(p));
        Assert.Contains("Troll", troll);
        Assert.DoesNotContain("Draugr_Elite", troll);
        Assert.DoesNotContain("Draugr_Elite", Prefabs(Crypt(s => s with { Boss = "" }), p => !Spawner(p)));
    }

    [Fact]
    public void ChosenFoesAndNoRespawnHoldInEveryRoom()
    {
        // The elites were the biome's whatever the foes chosen, a swamp burial chamber's foes sleeping draugr, and an
        // ossuary's bone pile raised the dead with Respawn off.
        var others = DungeonKit.Biomes.SelectMany(b => b.Foes.Concat(b.Elites)).SelectMany(f => new[] { f.Spawner, f.Creature })
            .Concat(["Draugr_sleeping", "Draugr_Ranged_sleeping", "Spawner_DraugrPile", "BonePileSpawner", "BonePileSpawner_swamp"]).ToHashSet();
        int wolves = 0;
        for (int seed = 1; seed <= 4; seed++)
        {
            var r = DungeonGen.Make(new DungeonGen.Settings(Biome: "Swamp", Style: "Crypt", Size: 3, Levels: 2, Seed: seed, Foes: ["Wolf"], Respawn: false, Boss: ""));
            Assert.DoesNotContain(r.Items, i => others.Contains(i.Prefab));
            wolves += r.Items.Count(i => i.Prefab == "Wolf");
        }
        Assert.True(wolves > 0);
    }

    [Fact]
    public void FewerLightsWithoutLightAndLessRubbleWithoutDecay()
    {
        static int Lights(DungeonGen.Result r) => r.Items.Count(i => i.Prefab.Contains("torch", StringComparison.Ordinal) || i.Prefab.Contains("brazier", StringComparison.OrdinalIgnoreCase));
        var plain = Crypt();
        Assert.True(Lights(Crypt(s => s with { Light = 0 })) < Lights(plain));
        Assert.True(Crypt(s => s with { Decay = 0 }).Items.Count < plain.Items.Count);
        Assert.True(Crypt(s => s with { Decay = 1 }).Items.Count > plain.Items.Count);
    }

    [Fact]
    public void ASmallCryptWithoutRoomForItsArenaSaysSo()
    {
        // Small crypts of two levels: some seeds leave no room on the last level for the antechamber and the arena.
        var made = Enumerable.Range(1, 80).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Meadows", Style: "Crypt", Size: 1, Levels: 2, Seed: seed))).ToList();
        Assert.Contains(made, r => r.Notes.Exists(n => n.English == "no room for the boss arena"));
        Assert.Contains(made, r => !r.Notes.Exists(n => n.English == "no room for the boss arena"));
    }

    [Fact]
    public void TheLightAndDecaySettingsChangeNothingElse()
    {
        // The same seed, lit otherwise: everything that is not a light stands where it stood. Decayed otherwise:
        // everything that is not rubble (nor a thing decay breaks or puts out) stands where it stood.
        static HashSet<(string, Vector3)> Rest(DungeonGen.Result r, params string[] not) =>
            [.. r.Items.Where(i => !not.Any(n => i.Prefab.Contains(n, StringComparison.OrdinalIgnoreCase))).Select(i => (i.Prefab, i.Position))];
        static void Same(HashSet<(string, Vector3)> a, HashSet<(string, Vector3)> b, string what)
        {
            var differ = a.Except(b).Select(x => $"only without: {x}").Concat(b.Except(a).Select(x => $"only with: {x}")).ToList();
            Assert.True(differ.Count == 0, what + ": " + string.Join("; ", differ.Take(8)));
        }
        string[] lights = ["torch", "brazier", "lantern", "Candle"];
        foreach (string style in new[] { "Crypt", "Fortress", "Catacombs" })
        {
            var dark = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style, Size: 3, Levels: 1, Seed: 5, Light: 0));
            var bright = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style, Size: 3, Levels: 1, Seed: 5, Light: 2));
            Assert.NotEqual(dark.Items.Count, bright.Items.Count);
            Same(Rest(dark, lights), Rest(bright, lights), style + " light");
            string[] decay = ["Rubble", "Stonepile", "_ruin", "_cracked", "web", "Statue", "stone_pillar", "torch", "brazier", "Pickable_StoneRock"];
            var kept = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style, Size: 3, Levels: 1, Seed: 5, Decay: 0));
            var ruined = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style, Size: 3, Levels: 1, Seed: 5, Decay: 1));
            Assert.True(kept.Items.Count < ruined.Items.Count);
            Same(Rest(kept, decay), Rest(ruined, decay), style + " decay");
        }
    }
}
