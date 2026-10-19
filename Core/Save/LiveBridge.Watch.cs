using System.Text.Json;

namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>A zone's two watch numbers, as the plugin gives them.</summary>
    /// <param name="Still">
    /// Changes when an object of the zone is made or removed, or one that stays put changes its data or place: the
    /// zone is read whole again (<see cref="ZoneObjects"/>).
    /// </param>
    /// <param name="Moving">
    /// Changes when an object of a kind that moves or ticks on its own (a creature, a ship, an item, a fire...) moves
    /// or changes its data: those objects are read alone (<see cref="MovingObjects"/>). Plugins from before 1.57.2
    /// give none: 0, never changing.
    /// </param>
    public readonly record struct Digests(ulong Still, ulong Moving);

    /// <summary>
    /// The watch numbers of each zone (a plugin from 1.17.0 on; the second number from 1.57.2 on, 0 before).
    /// </summary>
    public async Task<Dictionary<(int X, int Z), Digests>> Watch(IReadOnlyCollection<(int X, int Z)> zones)
    {
        using var res = await Post("watch", ZoneList(zones));
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var digests = new Dictionary<(int, int), Digests>();
        foreach (var z in doc.RootElement.GetProperty("zones").EnumerateArray())
        {
            ulong moving = z.GetArrayLength() > 3 ? Number(z[3]) : 0;
            digests[(z[0].GetInt32(), z[1].GetInt32())] = new Digests(Number(z[2]), moving);
        }
        return digests;
    }

    /// <summary>A watch number: a 64-bit unsigned the plugin writes as a string.</summary>
    private static ulong Number(JsonElement e) => ulong.Parse(e.GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The saved objects of the zones as the game has them now: ZDOID and save-format bytes.</summary>
    /// <exception cref="InvalidDataException">The answer is damaged or unpacks to more than a world snapshot can.</exception>
    public async Task<List<(long User, uint Id, byte[] Bytes)>> ZoneObjects(IReadOnlyCollection<(int X, int Z)> zones) => await Objects("zone", zones);

    /// <summary>
    /// Of the zones' saved objects, only those of the kinds that move or tick on their own, as the game has them now
    /// (a plugin from 1.57.2 on; older ones answer 404, "too old": the editor asks only once a zone's second watch
    /// number changed, which theirs never does).
    /// </summary>
    /// <exception cref="InvalidDataException">The answer is damaged or unpacks to more than a world snapshot can.</exception>
    public async Task<List<(long User, uint Id, byte[] Bytes)>> MovingObjects(IReadOnlyCollection<(int X, int Z)> zones) => await Objects("zone/movers", zones);

    /// <summary>Objects of the zones as a zone endpoint sends them: ZDOID and save-format bytes, gzip-compressed.</summary>
    private async Task<List<(long User, uint Id, byte[] Bytes)>> Objects(string path, IReadOnlyCollection<(int X, int Z)> zones)
    {
        using var res = await Post(path, ZoneList(zones));
        // Some zones' objects: never more than the whole world's snapshot unpacks to.
        using var raw = new MemoryStream(CheckedReads.Gunzip(await res.Content.ReadAsByteArrayAsync(), WorldSave.MaxSnapshot));
        using var r = new BinaryReader(raw);
        // Each: ZDOID (8 + 4), then the bytes' length (4).
        int count = r.CheckCount(r.ReadInt32(), 16);
        var list = new List<(long, uint, byte[])>(count);
        for (int i = 0; i < count; i++)
        {
            long user = r.ReadInt64();
            uint id = r.ReadUInt32();
            list.Add((user, id, r.ReadExactBytes(r.ReadInt32())));
        }
        return list;
    }

    /// <summary>Takes a snapshot and loads it as a world, its source named after the bridge address.</summary>
    public async Task<WorldSave> LoadWorld() => WorldSave.LoadLive(await Snapshot(), "live: " + Url);
}
