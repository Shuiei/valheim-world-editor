
namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>
    /// Objects to destroy (live ZDOIDs) and to create (save-format bytes); returns the plugin's JSON. After them, each
    /// destroyed object's prefab and position: another mod may have made the object again under a new ZDOID
    /// (ServersideQoL does, for build pieces, plants, fires and more), and the plugin finds it by those. Plugins from
    /// before 1.15.4 stop reading before them. Then the moves, only for a plugin that can (<see cref="Abilities.CanMove"/>):
    /// one that cannot would leave them out.
    /// </summary>
    /// <param name="destroy">The objects to destroy.</param>
    /// <param name="create">The objects to make, in the save format.</param>
    /// <param name="moves">The objects to move (none: the call older plugins take).</param>
    /// <param name="callId">
    /// The call's id, for a plugin that can (<see cref="Abilities.CanCallOnce"/>): the same call sent again under it after its
    /// answer was lost is not done twice. Null: none sent.
    /// </param>
    public async Task<string> ApplyObjects(IReadOnlyList<LiveTarget> destroy, IReadOnlyList<byte[]> create, IReadOnlyList<LiveMove>? moves = null, string? callId = null)
    {
        byte[] call = Build(w =>
        {
            w.Write(destroy.Count);
            foreach (LiveTarget t in destroy)
            {
                w.Write(t.User);
                w.Write(t.Id);
            }
            w.Write(create.Count);
            foreach (byte[] o in create)
            {
                w.Write(o.Length);
                w.Write(o);
            }
            foreach (LiveTarget t in destroy)
            {
                w.Write(t.Prefab);
                Write(w, t.Position);
            }
            if (moves is { Count: > 0 })
            {
                w.Write(moves.Count);
                foreach (LiveMove m in moves)
                {
                    w.Write(m.From.User);
                    w.Write(m.From.Id);
                    w.Write(m.From.Prefab);
                    Write(w, m.From.Position);
                    Write(w, m.Position);
                    Write(w, m.Rotation);
                    w.Write(m.Fallback.Length);
                    w.Write(m.Fallback);
                }
            }
        });
        using var res = await Send(HttpMethod.Post, "objects", call, callId);
        string body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"bridge objects: {(int)res.StatusCode} {body}");
        }
        return body;
    }

    /// <summary>
    /// Zones to hand back to the world generator in the running game: int count, then per zone int x, int z, bool keep
    /// buildings, bool reset ground. Returns the plugin's JSON.
    /// </summary>
    public async Task<string> ResetZones(IReadOnlyList<Editing.ZoneReset> zones)
    {
        using var res = await Send(HttpMethod.Post, "zones/reset", Build(w =>
        {
            w.Write(zones.Count);
            foreach (var z in zones)
            {
                w.Write(z.X);
                w.Write(z.Z);
                w.Write(z.KeepBuildings);
                w.Write(z.Ground);
            }
        }));
        string body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(res.StatusCode == System.Net.HttpStatusCode.NotFound
                ? "the WorldEditorBridge plugin on the server is too old to reset zones live: update it"
                : $"bridge zones/reset: {(int)res.StatusCode} {body}");
        }
        return body;
    }

    /// <summary>The zones asked for as an int count, then int x, int z each.</summary>
    private static byte[] ZoneList(IReadOnlyCollection<(int X, int Z)> zones) => Build(w =>
    {
        w.Write(zones.Count);
        foreach (var (x, z) in zones)
        {
            w.Write(x);
            w.Write(z);
        }
    });
}
