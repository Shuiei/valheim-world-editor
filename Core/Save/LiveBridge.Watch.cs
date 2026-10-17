using System.Text.Json;

namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>
    /// One number per zone that changes when an object there changes (a plugin from this version on).
    /// </summary>
    public async Task<Dictionary<(int X, int Z), ulong>> Watch(IReadOnlyCollection<(int X, int Z)> zones)
    {
        using var res = await Post("watch", ZoneList(zones));
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var digests = new Dictionary<(int, int), ulong>();
        foreach (var z in doc.RootElement.GetProperty("zones").EnumerateArray())
        {
            digests[(z[0].GetInt32(), z[1].GetInt32())] = ulong.Parse(z[2].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        }
        return digests;
    }

    /// <summary>The saved objects of the zones as the game has them now: ZDOID and save-format bytes.</summary>
    /// <exception cref="InvalidDataException">The answer is damaged or unpacks to more than a world snapshot can.</exception>
    public async Task<List<(long User, uint Id, byte[] Bytes)>> ZoneObjects(IReadOnlyCollection<(int X, int Z)> zones)
    {
        using var res = await Post("zone", ZoneList(zones));
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
