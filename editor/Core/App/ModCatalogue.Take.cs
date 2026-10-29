using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Taking a game's report of its mod prefabs into what is known, and leaving out the prefabs that are the game's own.
/// </summary>
public static partial class ModCatalogue
{
    /// <summary>
    /// Takes a game's report (GET /catalogue): its prefabs that are not the game's own (<paramref name="vanilla"/>)
    /// join those known, replacing their older entries, those known that are the game's own now are left out, and
    /// the whole is kept. Entries not in the editor's format
    /// are left out.
    /// </summary>
    /// <param name="report">The bridge's JSON.</param>
    /// <param name="vanilla">The game's own prefab names (GameData's prefabs).</param>
    /// <returns>How many mod prefabs the report has, and whether what is known changed.</returns>
    /// <exception cref="JsonException">The report is not JSON, or not an object.</exception>
    public static (int Mods, bool Changed) Take(string report, IReadOnlySet<string> vanilla)
    {
        var got = Read(report);
        lock (Writing)
        {
            var now = _data;
            var next = new Data(new(now.Prefabs), new(now.Pieces), new(now.Costs), new(now.Items), new(now.Mods), new(now.Models));
            int mods = 0;
            bool changed = false;
            foreach (var (name, entry) in got.Prefabs)
            {
                if (vanilla.Contains(name))
                {
                    continue;
                }
                mods++;
                changed |= Put(next.Prefabs, name, entry);
                changed |= got.Pieces.TryGetValue(name, out var piece) ? Put(next.Pieces, name, piece) : next.Pieces.Remove(name);
                changed |= got.Costs.TryGetValue(name, out var cost) ? Put(next.Costs, name, cost) : next.Costs.Remove(name);
                if (got.Mods.TryGetValue(name, out string? mod) && next.Mods.GetValueOrDefault(name) != mod)
                {
                    next.Mods[name] = mod;
                    changed = true;
                }
            }
            changed |= DropVanilla(next, vanilla);
            HashSet<string> required = Required(next.Costs.Values);
            foreach (var (item, name) in got.Items)
            {
                if (required.Contains(item) && next.Items.GetValueOrDefault(item) != name)
                {
                    next.Items[item] = name;
                    changed = true;
                }
            }
            if (changed)
            {
                // Kept first: should writing fail, nothing is in use that a retry would take for already known.
                SafeFile.WriteAllText(FilePath, Write(next));
                Use(next);
            }
            return (mods, changed);
        }
    }

    /// <summary>
    /// Leaves out the prefabs that are the game's own (a game update made a mod's name vanilla), with all that is
    /// known of them; whether any was there.
    /// </summary>
    private static bool DropVanilla(Data d, IReadOnlySet<string> vanilla)
    {
        bool dropped = false;
        foreach (string name in d.Prefabs.Keys.Where(vanilla.Contains).ToList())
        {
            d.Prefabs.Remove(name);
            d.Pieces.Remove(name);
            d.Costs.Remove(name);
            d.Mods.Remove(name);
            d.Models.Remove(name);
            dropped = true;
        }
        return dropped;
    }

    /// <summary>Every item some cost entry takes.</summary>
    private static HashSet<string> Required(IEnumerable<JsonNode> costs) =>
        costs.SelectMany(c => c["r"]!.AsArray()).Select(r => r![0]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
}
