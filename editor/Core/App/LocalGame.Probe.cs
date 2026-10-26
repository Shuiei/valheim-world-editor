using System.Text.Json;

namespace TerrainEditor.App;

/// <summary>
/// Asking the player's running game whether its bridge answers: the probe of each bridge's /status, on this
/// computer only and with the bridge's token.
/// </summary>
public static partial class LocalGame
{
    /// <summary>
    /// Why each port's last probe failed, by port: a failure is logged when it differs from the last one (the start page
    /// asks every 2.5 seconds), and an answer clears it.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> ProbeFailures = new();

    /// <summary>One client for every probe, with the 1.5 second wait.</summary>
    private static readonly HttpClient Prober = new() { Timeout = TimeSpan.FromSeconds(1.5) };

    /// <summary>
    /// Which bridge (if any) answers on this computer, and with which world: the first, in the order given, whose
    /// /status on 127.0.0.1 answers within 1.5 seconds. All are asked at once, so silent ports cost 1.5 s in all.
    /// </summary>
    public static async Task<Running?> FindRunning(IEnumerable<Bridge> bridges)
    {
        Running?[] answers = await Task.WhenAll(bridges.DistinctBy(b => (b.Port, b.Token)).Select(Probe));
        return Array.Find(answers, a => a != null);
    }

    /// <summary>The bridge's /status, or null when it does not answer within 1.5 seconds or refuses.</summary>
    private static async Task<Running?> Probe(Bridge b)
    {
        try
        {
            using HttpRequestMessage req = new(HttpMethod.Get, $"http://127.0.0.1:{b.Port}/status");
            req.Headers.Add("X-Bridge-Token", b.Token);
            using HttpResponseMessage res = await Prober.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                return null;
            }
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            string world = doc.RootElement.TryGetProperty("world", out var w) ? w.GetString() ?? "" : "";
            int players = doc.RootElement.TryGetProperty("players", out var pl) && pl.TryGetInt32(out int n) ? n : 0;
            ProbeFailures.TryRemove(b.Port, out _);
            return new Running(b, world, players);
        }
        catch (Exception e)
        {
            // A bridge that does not answer is not running here. Said when it changes, not every 2.5 seconds.
            string why = e.GetType().FullName + ": " + e.Message;
            if (!ProbeFailures.TryGetValue(b.Port, out string? last) || last != why)
            {
                ProbeFailures[b.Port] = why;
                Log.CaughtBrief(e, $"asking the bridge on port {b.Port} whether the game runs (not running there; said again only when this changes)");
            }
            return null;
        }
    }
}
