using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>list_locations: the world's locations from its zone file (or, without one, those of zones generated).</summary>
    [McpServerTool(Name = "list_locations", ReadOnly = true, Title = "The world's locations")]
    [Description("The locations the game laid out in the open world: the start, the traders (Haldor, Hildir, the Bog Witch), the bosses' altars (Eikthyr, the Elder, Bonemass, Moder, Yagluth, the Queen, Fader), dungeon entrances (burial chambers, troll caves, sunken crypts, frost caves, infested mines…), villages, towers, runestones and the rest: game name, what players call it, kind (start, trader, boss, dungeon, other), world x, y, z, and whether its zone is generated yet (visited in game). Filter by words of the name, what or kind (\"boss\", \"trader\", \"Haldor\", \"crypt\"), and/or near a point; nearest first when near a point. Use it to find where to open_area.")]
    public async Task<string> ListLocations(
        [Description("Words to match in the name, what players call it or its kind (optional).")] string? query = null,
        [Description("World x of a point to look near (optional, with z).")] float? x = null,
        [Description("World z of that point.")] float? z = null,
        [Description("How far from it, metres (default 2000).")] float radius = 2000,
        [Description("At most this many (default 100).")] int limit = 100)
    {
        if (LoneCoordinate(x, z) is string lone)
        {
            return lone;
        }
        await GameRules();
        return await OnUi(() => Locations(query, x, z, radius, limit));
    }

    /// <summary>list_locations' answer, once the game's rules are read (on the window's thread).</summary>
    private string Locations(string? query, float? x, float? z, float radius, int limit)
    {
        if (_w.World?.World is not { } world)
        {
            return "Open a world first (open_world).";
        }
        var rules = ValheimGen.SeedLocations.Current.locations.GroupBy(r => Save.StableHash.Of(r.prefab)).ToDictionary(g => g.Key, g => g.First().prefab);
        var all = world.Zones is { Locations.Count: > 0 } zones
            ? zones.Locations.Select(l => (l.Hash, l.Position, Generated: (bool?)l.Placed))
            : world.Locations.Select(l => (Hash: l.Location, l.Position, Generated: (bool?)true));
        string[] words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var found = all.Where(l => !Editing.Uplift.IsDisc(l.Hash)).Select(l =>
        {
            string name = LocationName(l.Hash, rules) ?? l.Hash.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var (what, kind) = Known.TryGetValue(name, out var k) ? k : (null!, "other");
            return (Name: name, What: what, Kind: kind, l.Position, l.Generated);
        }).Where(l => words.All(w => l.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || (l.What?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false)
            || l.Kind.Equals(w, StringComparison.OrdinalIgnoreCase) || (l.Kind + "es").Equals(w, StringComparison.OrdinalIgnoreCase) || (l.Kind + "s").Equals(w, StringComparison.OrdinalIgnoreCase)));
        if (x is float px && z is float pz)
        {
            found = found.Where(l => (l.Position.X - px) * (l.Position.X - px) + (l.Position.Z - pz) * (l.Position.Z - pz) <= radius * radius)
                .OrderBy(l => (l.Position.X - px) * (l.Position.X - px) + (l.Position.Z - pz) * (l.Position.Z - pz));
        }
        else
        {
            // The ones players look for first, then by name.
            found = found.OrderBy(l => l.Kind switch { "start" => 0, "boss" => 1, "trader" => 2, "dungeon" => 3, _ => 4 }).ThenBy(l => l.Name, StringComparer.Ordinal);
        }
        var list = found.ToList();
        return ToJson(new
        {
            count = list.Count,
            locations = list.Take(Math.Clamp(limit, 1, 2000)).Select(l => new
            {
                name = l.Name,
                what = l.What,
                kind = l.Kind,
                x = MathF.Round(l.Position.X, 1),
                y = MathF.Round(l.Position.Y, 1),
                z = MathF.Round(l.Position.Z, 1),
                generated = l.Generated,
            }),
        });
    }
}
