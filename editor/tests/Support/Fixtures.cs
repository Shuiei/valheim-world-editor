using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Where the test fixtures are (editor/tests/fixtures, found up from the test binaries), and the game data and location rules
/// given from them.
/// </summary>
public static class Fixtures
{
    public static string Root
    {
        get
        {
            for (string? d = AppContext.BaseDirectory; d != null; d = Path.GetDirectoryName(d))
            {
                string f = Path.Combine(d, "tests", "fixtures");
                if (Directory.Exists(f))
                {
                    return f;
                }
            }
            throw new DirectoryNotFoundException("editor/tests/fixtures not found above " + AppContext.BaseDirectory);
        }
    }

    public static string World => Path.Combine(Root, "CITest");

    /// <summary>
    /// The test world as Valheim 1.0.20 saved it, in save format 42: CITest loaded by its dedicated server, one object of
    /// each chunk file moved and moved back through the bridge (so the game wrote every chunk file again), then saved.
    /// Its files differ from CITest's only by the version in each file's head, the chunk files' and save's numbers, and
    /// the random events' timer.
    /// </summary>
    public static string World42 => Path.Combine(Root, "CITest42");

    /// <summary>
    /// The game's location rules for the kinds laid out first (start, bosses, traders), as `ValheimWorldEditor
    /// --export-locations` reads them from Valheim 0.221: CI has no game.
    /// </summary>
    public static void UseLocations() =>
        ValheimGen.SeedLocations.Use(ValheimGen.SeedLocations.RuleSet.FromJson(File.ReadAllText(Path.Combine(Root, "locations.json"))));

    /// <summary>
    /// The game's alt biomes with every field the layout reads, as Valheim 0.221 has them (editor/tests/fixtures/altbiomes.json,
    /// read from the game by GameLocations: CI has no game).
    /// </summary>
    public static List<ValheimGen.SeedLocations.AltBiomeRule> AltBiomes() =>
        System.Text.Json.JsonSerializer.Deserialize<List<ValheimGen.SeedLocations.AltBiomeRule>>(File.ReadAllText(Path.Combine(Root, "altbiomes.json")))!;

    /// <summary>
    /// The game's data (terrain modifiers, object data names...) trimmed to what the tests meet, as `ValheimWorldEditor
    /// --export-test-data editor/tests/fixtures` reads it from the game: the editor reads it from the player's Valheim, which
    /// CI does not have. Given before any test runs, as the editor has it before any world opens.
    /// </summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void UseGameData() => TerrainEditor.App.GameData.UseCopies(Root);

    // The same world as the running game sends it (WorldEditorBridge /snapshot, gzip).
    public static string Snapshot => Path.Combine(Root, "CITest.snapshot");

    public static int Hash(string name) => StableHash.Of(name);

    /// <summary>
    /// The test world (editor/tests/fixtures/CITest) copied where saving cannot harm it.
    /// </summary>
    internal static string CopyFixture()
    {
        string? root = AppContext.BaseDirectory;
        while (root != null && !Directory.Exists(Path.Combine(root, "tests", "fixtures", "CITest")))
        {
            root = Path.GetDirectoryName(root);
        }
        Assert.NotNull(root);
        string dir = Path.Combine(Path.GetTempPath(), "vwe-native-" + Guid.NewGuid().ToString("N")[..10], "CITest");
        Directory.CreateDirectory(dir);
        foreach (string f in Directory.GetFiles(Path.Combine(root, "tests", "fixtures", "CITest")))
        {
            File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
        }
        return dir;
    }
}
