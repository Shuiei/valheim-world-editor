using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public static partial class Dungeons
{
    /// <summary>Reads the scan's JSON ("rooms" and "dungeons", each by prefab name).</summary>
    private static Catalog Load(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var rooms = new Dictionary<int, Room>();
        foreach (JsonProperty p in doc.RootElement.GetProperty("rooms").EnumerateObject())
        {
            JsonElement r = p.Value;
            bool B(string k) => r.GetProperty(k).GetInt32() != 0;
            var (contents, spawns, picks, nodes) = Parts(r);
            rooms[StableHash.Of(p.Name)] = new Room(p.Name, StableHash.Of(p.Name), r.GetProperty("theme").GetInt32(), V(r.GetProperty("size"), 0),
                B("endCap"), B("entrance"), B("divider"), r.GetProperty("endCapPrio").GetInt32(), r.GetProperty("weight").GetSingle(), B("perimeter"), B("enabled"),
                [.. r.GetProperty("openings").EnumerateArray().Select(o => new Opening(o[0].GetString() ?? "", o[1].GetInt32() != 0, o[2].GetInt32() != 0, V(o, 4), Q(o, 7), o[3].GetInt32() != 0))],
                contents, spawns, picks, nodes);
        }
        var kinds = new Dictionary<int, Kind>();
        foreach (JsonProperty p in doc.RootElement.GetProperty("dungeons").EnumerateObject())
        {
            JsonElement d = p.Value;
            kinds[StableHash.Of(p.Name)] = new Kind(p.Name, StableHash.Of(p.Name), d.GetProperty("themes").GetInt32(), d.GetProperty("algorithm").GetInt32() == 0, V(d.GetProperty("zoneSize"), 0),
                d.GetProperty("customInterior").GetInt32() != 0, d.GetProperty("baseSeed").GetInt32() != 0)
            {
                Doors = [.. d.GetProperty("doors").EnumerateArray().Select(x => (x[0].GetString() ?? "", x[1].GetString() ?? "", x[2].GetSingle()))],
                DoorChance = d.GetProperty("doorChance").GetSingle(),
            };
        }
        return new Catalog(rooms, kinds);
    }

    /// <summary>A content's snap-to-ground offset, after its node; null when it has none.</summary>
    private static float? SnapOf(JsonElement c) => c.GetArrayLength() > 9 ? c[9].GetSingle() : null;

    /// <summary>
    /// The contents, spawns, picks and nodes of a room's or a location's JSON entry (what the scan writes of a
    /// prefab's tree).
    /// </summary>
    internal static (Content[] Contents, Spawn[] Spawns, Pick[] Picks, int[] Nodes) Parts(JsonElement r)
    {
        return ([.. r.GetProperty("contents").EnumerateArray().Select(c => new Content(c[0].GetString() ?? "", V(c, 1), Q(c, 4), c[8].GetInt32(), SnapOf(c)))],
            [.. r.GetProperty("spawns").EnumerateArray().Select(x => new Spawn(x[0].GetInt32(), x[1].GetSingle(), x[2].GetInt32(), x[3].GetInt32(), x[4].GetInt32(), x[5].GetInt32()))],
            [.. r.GetProperty("picks").EnumerateArray().Select(x => new Pick(x[0].GetInt32(), x[1].GetInt32(),
                [.. x[2].EnumerateArray().Select(c => (c[0].GetInt32(), c[1].GetSingle()))], x[3].GetInt32(), x[4].GetInt32()))],
            [.. r.GetProperty("nodes").EnumerateArray().Select(n => n.GetInt32())]);
    }
}
