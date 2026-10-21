using System.Net;
using System.Numerics;

namespace WorldEditor.Tests;

/// <summary>
/// The stand-in plugin's answer to the objects call: the objects destroyed and created, their places and the moves.
/// </summary>
public sealed partial class FakeBridge
{
    /// <summary>
    /// Reads an objects call, records it and returns its answer (also kept under the call id, when it has one).
    /// </summary>
    private async Task<string> ServeObjects(HttpListenerContext ctx)
    {
        using var all = new MemoryStream();
        await ctx.Request.InputStream.CopyToAsync(all);
        all.Position = 0;
        using var r = new BinaryReader(all);
        int destroy = r.ReadInt32();
        for (int i = 0; i < destroy; i++)
        {
            Destroyed.Add((r.ReadInt64(), r.ReadUInt32()));
        }
        var ids = ObjectsCallReader.ReadCreated(r, () => $"77:{_next++}");
        // What each destroyed object is and where (the editor adds it after the creations).
        for (int i = 0; i < destroy; i++)
        {
            Places.Add((r.ReadInt32(), new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle())));
        }
        var moved = ReadMoves(r, all);
        lock (ObjectCalls)
        {
            ObjectCalls.Add((destroy, ids.Count));
        }
        string body = $"{{\"destroyed\":{destroy},\"missing\":0,\"refound\":{Refound},\"created\":[{string.Join(",", ids.Select(i => $"\"{i}\""))}],\"moves\":[{string.Join(",", moved)}]}}";
        if (ctx.Request.Headers["X-Call-Id"] is { } call)
        {
            _answers[call] = body;
        }
        return body;
    }

    /// <summary>
    /// Reads the moves at the end of an objects call (none when the call ends before them) and records them, with whether
    /// each takes its new data (false when the call does not say). Each object moved in place keeps its id, one made
    /// again gets a new one.
    /// </summary>
    private List<string> ReadMoves(BinaryReader r, MemoryStream all)
    {
        var moved = new List<string>();
        if (all.Position < all.Length)
        {
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                long user = r.ReadInt64();
                uint id = r.ReadUInt32();
                int prefab = r.ReadInt32();
                Vector3 V() => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                Vector3 from = V(), to = V(), turn = V();
                MoveBytes.Add(r.ReadBytes(r.ReadInt32()));
                Moves.Add((user, id, prefab, from, to, turn));
                moved.Add(MoveInPlace ? $"{{\"id\":\"{user}:{id}\",\"inPlace\":true}}" : $"{{\"id\":\"77:{_next++}\",\"inPlace\":false}}");
            }
            bool[] data = new bool[n];
            if (all.Position < all.Length)
            {
                int k = r.ReadInt32();
                for (int i = 0; i < k; i++)
                {
                    bool d = r.ReadBoolean();
                    if (i < n)
                    {
                        data[i] = d;
                    }
                }
            }
            MoveData.AddRange(data);
        }
        return moved;
    }
}
