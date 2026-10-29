using System.Text.Json;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.Terrain;

/// <summary>
/// The random draws a new object of each prefab makes as it wakes, read from the player's own Valheim (GameData; the
/// tests use editor/tests/fixtures/wake-draws.json): its scripts' draws in the order they wake. A draw an object makes shifts
/// every draw after it, so whatever the game draws next for a location or a vegetation rule (the next object's swap,
/// data, chance, place) follows from them. <see cref="Wake(string, Waking)"/> makes them on the game's random state replica.
/// </summary>
public static class WakeDraws
{
    /// <summary>What one script, or a run of scripts, draws.</summary>
    /// <param name="Kind">Which.</param>
    /// <param name="Count">The draws of a run (<see cref="PartKind.Draws"/>).</param>
    /// <param name="Loot">A container's default items (<see cref="PartKind.Loot"/>).</param>
    public sealed record Part(PartKind Kind, int Count = 0, Loot? Loot = null);

    /// <summary>The kinds of <see cref="Part"/>.</summary>
    public enum PartKind
    {
        /// <summary>That many draws, whatever happens.</summary>
        Draws,
        /// <summary>A building piece's WearNTear: what it draws depends on its health (the caller says).</summary>
        Wear,
        /// <summary>
        /// A LodFadeInOut beside a LODGroup: a draw when the camera is more than 20 m away, which it is for the objects of
        /// new zones (on a dedicated server too: checked against a server's world).
        /// </summary>
        Lod,
        /// <summary>A snow-melting fire: a draw in the Deep North only.</summary>
        Melt,
        /// <summary>A container's default items: how many draws depends on the draws.</summary>
        Loot,
    }

    /// <summary>A container's default items, as their draws need them (the game's DropTable).</summary>
    /// <param name="Chance">The chance of any item.</param>
    /// <param name="Min">The fewest picks.</param>
    /// <param name="Max">The most picks.</param>
    /// <param name="Each">Whether an item is picked once at most.</param>
    /// <param name="Weights">Each item's weight.</param>
    public sealed record Loot(float Chance, int Min, int Max, bool Each, IReadOnlyList<float> Weights);

    /// <summary>How an object wakes, besides its prefab.</summary>
    /// <param name="Wear">Makes a building piece's draws (its WearNTear's).</param>
    /// <param name="DeepNorth">Whether it stands in the Deep North.</param>
    /// <param name="Looted">Whether its data says its default items were added already (none drawn).</param>
    public readonly record struct Waking(Action Wear, bool DeepNorth = false, bool Looted = false);

    /// <summary>Each prefab's parts, by name; none until Use.</summary>
    private static volatile Dictionary<string, IReadOnlyList<Part>> _byName = new(StringComparer.Ordinal);

    /// <summary>The data key that says a container's default items were added.</summary>
    private static readonly int LootedKey = Save.StableHash.Of("addedDefaultItems");

    /// <summary>Whether an object's data says its default items were added (a container then draws none); the last value wins.</summary>
    public static bool Looted(IReadOnlyList<ValheimGen.ContentField>? data) =>
        data?.LastOrDefault(f => f.Section == "ints" && f.Key == LootedKey) is { Value: not "0" };

    /// <summary>
    /// Takes the prefabs' draws as GameCatalogues' WakeScan writes them: by name, a list of numbers (draws), "wear",
    /// "lod", "melt" and containers' default items ({"chance", "min", "max", "each", "weights"}).
    /// </summary>
    public static void Use(string json) => _byName = Read(json);

    /// <summary>Each prefab's parts, by name, from the JSON <see cref="Use"/> takes.</summary>
    internal static Dictionary<string, IReadOnlyList<Part>> Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var byName = new Dictionary<string, IReadOnlyList<Part>>(StringComparer.Ordinal);
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            byName[p.Name] = [.. p.Value.EnumerateArray().Select(PartOf)];
        }
        return byName;
    }

    /// <summary>One part of a prefab's list.</summary>
    private static Part PartOf(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => new Part(PartKind.Draws, e.GetInt32()),
        JsonValueKind.String => new Part(e.GetString() switch
        {
            "wear" => PartKind.Wear,
            "lod" => PartKind.Lod,
            "melt" => PartKind.Melt,
            string s => throw new InvalidDataException($"unknown wake draw {s}"),
            null => throw new InvalidDataException("a wake draw without a name"),
        }),
        _ => new Part(PartKind.Loot, Loot: new Loot((float)e.GetProperty("chance").GetDouble(), e.GetProperty("min").GetInt32(), e.GetProperty("max").GetInt32(),
            e.GetProperty("each").GetBoolean(), [.. e.GetProperty("weights").EnumerateArray().Select(w => (float)w.GetDouble())])),
    };

    /// <summary>A prefab's parts; none for a prefab whose scripts draw nothing, or one unknown.</summary>
    public static IReadOnlyList<Part> Of(string prefab) => _byName.GetValueOrDefault(prefab) ?? [];

    /// <summary>Makes the draws a new object of this prefab makes as it wakes, on the game's random state replica.</summary>
    /// <param name="prefab">Its prefab.</param>
    /// <param name="how">How it wakes.</param>
    public static void Wake(string prefab, Waking how) => Wake(Of(prefab), how);

    /// <summary>Makes the draws of these parts, in order, on the game's random state replica.</summary>
    internal static void Wake(IReadOnlyList<Part> parts, Waking how)
    {
        foreach (var s in parts)
        {
            switch (s.Kind)
            {
                case PartKind.Draws:
                    Skip(s.Count);
                    break;
                case PartKind.Lod:
                    Skip(1);
                    break;
                case PartKind.Wear:
                    how.Wear();
                    break;
                case PartKind.Melt:
                    Skip(how.DeepNorth ? 1 : 0);
                    break;
                case PartKind.Loot:
                    DrawLoot(s.Loot!, how.Looted);
                    break;
                default:
                    throw new InvalidDataException($"unknown wake draw {s.Kind}");
            }
        }
    }

    /// <summary>
    /// The draws of a container's default items (none when its data says they were added): whether there are any (by
    /// the chance), how many picks, then for each
    /// pick a roll over the weights and, when it gives an item, its stack. An item picked once at most leaves the
    /// rolls after it; a roll beyond every weight gives the first item left.
    /// </summary>
    private static void DrawLoot(Loot loot, bool looted)
    {
        if (looted || Rnd.Value > loot.Chance)
        {
            return;
        }
        var weights = loot.Weights.ToList();
        float total = 0f;
        foreach (float w in weights)
        {
            total += w;
        }
        for (int picks = Rnd.Range(loot.Min, loot.Max + 1); picks > 0; picks--)
        {
            int at = Picked(weights, Rnd.Range(0f, total)); // nosemgrep
            if (at < 0)
            {
                continue;
            }
            // The item's stack.
            Skip(1);
            if (loot.Each && at < weights.Count)
            {
                total -= weights[at];
                weights.RemoveAt(at);
            }
        }
    }

    /// <summary>
    /// The item a roll picks: the first whose running weight reaches it; past them all the first one (taken but not
    /// removed: its index is the count); -1 when none is left.
    /// </summary>
    private static int Picked(List<float> weights, float roll)
    {
        float sum = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            sum += weights[i];
            if (roll <= sum)
            {
                return i;
            }
        }
        return weights.Count > 0 ? weights.Count : -1;
    }

    /// <summary>Makes this many draws, their values unused.</summary>
    public static void Skip(int n)
    {
        for (; n > 0; n--)
        {
            _ = Rnd.Value;
        }
    }
}
