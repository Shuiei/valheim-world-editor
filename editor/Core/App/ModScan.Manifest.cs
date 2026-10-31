using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// The pieces the add-ons' manifests describe (a prefab's "piece"), for mods that make their pieces in code, where no
/// bundle holds them.
/// </summary>
public static partial class ModScan
{
    /// <summary>
    /// Adds the pieces the manifests describe that are not the game's own nor read from a bundle already: a networked
    /// prefab, a hammer piece of the Building tab, its cost when the manifest gives one (its name always), and its mod
    /// (the manifest's). Hidden ones are left out.
    /// </summary>
    /// <param name="data">Where the pieces go.</param>
    /// <param name="vanilla">The game's own prefab names.</param>
    internal static void AddManifestPieces(Data data, IReadOnlySet<string> vanilla)
    {
        foreach (var (prefab, note) in ModManifests.Pieces)
        {
            if (note.Hidden || note.Piece is not { } piece || vanilla.Contains(prefab) || data.Mods.ContainsKey(prefab))
            {
                continue;
            }
            data.Entries["prefabs"][prefab] = new JsonObject { ["p"] = 1, ["d"] = 0, ["t"] = 2 };
            data.Entries["pieces"][prefab] = new JsonObject { ["cat"] = 2, ["comfort"] = 0, ["tool"] = "hammer" };
            var cost = new JsonObject { ["n"] = piece.Name, ["r"] = new JsonArray([.. piece.Cost.Select(c => (JsonNode)new JsonArray(c.Item, c.Amount, 1))]) };
            if (piece.Station != null)
            {
                cost["st"] = piece.Station;
            }
            data.Entries["piece-cost"][prefab] = cost;
            data.Mods[prefab] = note.Mod ?? Lang.En("build.otherMods");
        }
    }
}
