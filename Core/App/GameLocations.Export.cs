// The tests' location copy made again from the game (see GameLocations).

using ValheimGen;

namespace TerrainEditor.App;

public static partial class GameLocations
{
    /// <summary>
    /// The tests' copy (tests/fixtures/locations.json: the kinds laid out first) made again from the game:
    /// --export-locations &lt;file&gt;. Returns the exit code: 0 done, 1 no game or the rules not read, 2 no file
    /// named.
    /// </summary>
    public static int Export(string? path)
    {
        if (path == null)
        {
            Console.Error.WriteLine("--export-locations needs the file to write.");
            return 2;
        }
        var bundles = GameBundles.ForGame(GameLook.FindValheim(AppSettings.Load().ValheimPath), null);
        var rules = bundles == null ? null : Load(bundles, null);
        if (rules == null)
        {
            Console.Error.WriteLine(bundles == null ? "Valheim was not found." : "The location rules could not be read (see the log).");
            return 1;
        }
        var kept = SeedLocations.Prioritized(rules);
        File.WriteAllText(path, kept.ToJson());
        Console.WriteLine($"{kept.locations.Count} of {rules.locations.Count} locations written to {path}");
        return 0;
    }
}
