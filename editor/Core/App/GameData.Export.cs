using System.Text.Json;

namespace TerrainEditor.App;

// The tests' copies made again from the game, and the JSON text they are written as (see GameData).
public static partial class GameData
{
    /// <summary>
    /// A copy written one entry per line (an entry's value on its line, compact): small, and a game
    /// update shows in git entry by entry. A few groups of entries (piece-cost's items and pieces) are
    /// opened the same way.
    /// </summary>
    private static string Lines(JsonElement e, string indent = "")
    {
        string inner = indent + "  ";
        if (e.ValueKind == JsonValueKind.Object)
        {
            var props = e.EnumerateObject().ToList();
            bool groups = props.Count is > 0 and <= 4 && props.All(p => p.Value.ValueKind == JsonValueKind.Object);
            var lines = props.Select(p => inner + JsonSerializer.Serialize(p.Name) + ": " + (groups ? Lines(p.Value, inner) : p.Value.GetRawText()));
            return props.Count == 0 ? "{}" : "{\n" + string.Join(",\n", lines) + "\n" + indent + "}";
        }
        if (e.ValueKind == JsonValueKind.Array && e.GetArrayLength() > 0)
        {
            return "[\n" + string.Join(",\n", e.EnumerateArray().Select(x => inner + x.GetRawText())) + "\n" + indent + "]";
        }
        return e.GetRawText();
    }

    /// <summary>
    /// The tests' copies (&lt;folder&gt;/&lt;kind&gt;.json) made again from the game, each keeping the entries it
    /// already has: --export-test-data &lt;folder&gt;. Returns the exit code: 0 done, 1 no game, a missing copy or the
    /// game's data not read, 2 no such folder.
    /// </summary>
    public static int Export(string? folder)
    {
        if (folder == null || !Directory.Exists(folder))
        {
            Console.Error.WriteLine("--export-test-data needs the folder of the tests' copies (editor/tests/fixtures).");
            return 2;
        }
        // Closed when done: the game's files are not left open (Windows would refuse to touch them meanwhile).
        using var bundles = GameBundles.ForGame(GameLook.FindValheim(AppSettings.Load().ValheimPath), null);
        if (bundles == null)
        {
            Console.Error.WriteLine("Valheim was not found.");
            return 1;
        }
        string root = Path.GetFullPath(folder);
        if (Kinds.Select(k => KindPath(root, k.Name)).FirstOrDefault(p => !File.Exists(p)) is string missing)
        {
            Console.Error.WriteLine($"{missing} is missing.");
            return 1;
        }
        if (Load(bundles, cache: false) is not { } read)
        {
            Console.Error.WriteLine("The game's data could not be read (see the log).");
            return 1;
        }
        foreach (var kind in Kinds)
        {
            string path = KindPath(root, kind.Name);
            var (text, kept, now) = Trim(kind.Name, read[kind.Name], File.ReadAllText(path));
            File.WriteAllText(path, text);
            Console.WriteLine($"{path}: {now.Count} of {kept.Count} entries" + (now.Count < kept.Count ? $" (not in the game any more: {string.Join(", ", kept.Except(now))})" : ""));
        }
        return 0;
    }

    /// <summary>The copy of one kind inside the resolved export folder: &lt;root&gt;/&lt;kind&gt;.json.</summary>
    private static string KindPath(string root, string kindName) => Path.Combine(root, kindName + ".json");
}
