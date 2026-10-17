using System.Net;
using System.Text;

namespace WorldEditor.Tests;

/// <summary>
/// The stand-in game's answers to the players, terrain and objects requests.
/// </summary>
public sealed partial class FakeGame
{
    /// <summary>
    /// The players: counted, and held when HoldPlayers is set.
    /// </summary>
    private async Task ServePlayers(Reply reply)
    {
        Interlocked.Increment(ref _playerReads);
        if (HoldPlayers is { } hold)
        {
            await hold.Task;
        }
        reply.Body = Encoding.UTF8.GetBytes(Players);
    }

    /// <summary>
    /// The ground sent to the game: its patches are recorded, and held when HoldTerrain is set.
    /// </summary>
    private async Task ServeTerrain(HttpListenerContext ctx, Reply reply)
    {
        using var r = new BinaryReader(ctx.Request.InputStream);
        int n = r.ReadInt32();
        lock (Terrain)
        {
            for (int i = 0; i < n; i++)
            {
                int x = r.ReadInt32(), z = r.ReadInt32();
                r.ReadBytes(r.ReadInt32());
                Terrain.Add((x, z));
            }
        }
        if (HoldTerrain is { } hold)
        {
            TerrainArrived.TrySetResult();
            await hold.Task;
        }
        reply.Body = Encoding.UTF8.GetBytes($"{{\"zones\":{n}}}");
    }

    /// <summary>
    /// The objects sent to the game: the ones destroyed, and a new ZDOID for each one created.
    /// </summary>
    private void ServeObjects(HttpListenerContext ctx, Reply reply)
    {
        using var r = new BinaryReader(ctx.Request.InputStream);
        int destroy = r.ReadInt32();
        for (int i = 0; i < destroy; i++)
        {
            r.ReadInt64();
            r.ReadUInt32();
        }
        int create = r.ReadInt32();
        var ids = new List<string>();
        for (int i = 0; i < create; i++)
        {
            r.ReadBytes(r.ReadInt32());
            ids.Add($"\"77:{Interlocked.Increment(ref _next) - 1}\"");
        }
        lock (ObjectCalls)
        {
            ObjectCalls.Add((destroy, create));
        }
        reply.Body = Encoding.UTF8.GetBytes($"{{\"destroyed\":{destroy - Missing},\"missing\":{Missing},\"created\":[{string.Join(",", ids)}]}}");
    }
}
