using System.IO.Compression;
using System.Net;
using System.Text;

namespace WorldEditor.Tests;

/// <summary>
/// The stand-in game's answers to the zone requests: which zones changed (/watch), their objects (/zone), the objects
/// of theirs that move or tick on their own (/zone/movers) and the zones reset.
/// </summary>
public sealed partial class FakeGame
{
    /// <summary>
    /// The zones asked for: /watch gives their digests (both, or one as an older plugin does), /zone their objects
    /// (the movers among them) and /zone/movers their movers alone (gzipped).
    /// </summary>
    private void ServeZones(HttpListenerContext ctx, string path, Reply reply)
    {
        using var r = new BinaryReader(ctx.Request.InputStream);
        int n = r.ReadInt32();
        var asked = new List<(int, int)>();
        for (int i = 0; i < n; i++)
        {
            asked.Add((r.ReadInt32(), r.ReadInt32()));
        }
        lock (Zones)
        {
            if (path == "/watch")
            {
                // Zones not set are left out (as the plugin leaves out zones outside the world):
                // the editor then has nothing to read again there.
                reply.Body = Encoding.UTF8.GetBytes("{\"zones\":[" + string.Join(",", asked.Where(Zones.ContainsKey).Select(z => $"[{z.Item1},{z.Item2},\"{Zones[z].Digest}\"{MovingDigest(z)}]")) + "]}");
            }
            else
            {
                // /zone gives a zone's objects whole, its movers among them; /zone/movers its movers alone.
                bool movers = path == "/zone/movers";
                Interlocked.Increment(ref movers ? ref _moverReads : ref _zoneReads);
                var all = asked.SelectMany(z => Movers.TryGetValue(z, out var m) ? m.Objects : []).ToList();
                if (!movers)
                {
                    all.AddRange(asked.SelectMany(z => Zones.TryGetValue(z, out var e) ? e.Objects : []));
                }
                using var raw = new MemoryStream();
                using (var w = new BinaryWriter(raw, Encoding.UTF8, leaveOpen: true))
                {
                    w.Write(all.Count);
                    foreach (var (user, id, bytes) in all)
                    {
                        w.Write(user);
                        w.Write(id);
                        w.Write(bytes.Length);
                        w.Write(bytes);
                    }
                }
                using var packed = new MemoryStream();
                using (var gz = new GZipStream(packed, CompressionLevel.Fastest, leaveOpen: true))
                {
                    gz.Write(raw.ToArray());
                }
                reply.Body = packed.ToArray();
                reply.Type = "application/octet-stream";
            }
        }
    }

    /// <summary>
    /// A zone's second watch number as /watch writes it after the first (", number"): its movers' digest (0 with
    /// none), or nothing from a plugin from before 1.57.2.
    /// </summary>
    private string MovingDigest((int X, int Z) zone) => FollowsMovers ? $",\"{(Movers.TryGetValue(zone, out var m) ? m.Digest : 0)}\"" : "";

    /// <summary>
    /// The zones reset: each one asked for is recorded with its keep and ground flags.
    /// </summary>
    private void ServeZoneReset(HttpListenerContext ctx, Reply reply)
    {
        using var r = new BinaryReader(ctx.Request.InputStream);
        int n = r.ReadInt32();
        lock (Resets)
        {
            for (int i = 0; i < n; i++)
            {
                Resets.Add((r.ReadInt32(), r.ReadInt32(), r.ReadBoolean(), r.ReadBoolean()));
            }
        }
        reply.Body = Encoding.UTF8.GetBytes($"{{\"zones\":{n},\"destroyed\":0,\"locations\":0}}");
    }
}
