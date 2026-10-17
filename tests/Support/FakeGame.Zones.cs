using System.Net;
using System.IO.Compression;
using System.Text;

namespace WorldEditor.Tests;

/// <summary>
/// The stand-in game's answers to the zone requests: which zones changed (/watch), their objects (/zone) and the
/// zones reset.
/// </summary>
public sealed partial class FakeGame
{
    /// <summary>
    /// The zones asked for: /watch gives their digests, /zone their objects (gzipped).
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
                reply.Body = Encoding.UTF8.GetBytes("{\"zones\":[" + string.Join(",", asked.Where(Zones.ContainsKey).Select(z => $"[{z.Item1},{z.Item2},\"{Zones[z].Digest}\"]")) + "]}");
            }
            else
            {
                Interlocked.Increment(ref _zoneReads);
                var all = asked.SelectMany(z => Zones.TryGetValue(z, out var e) ? e.Objects : []).ToList();
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
