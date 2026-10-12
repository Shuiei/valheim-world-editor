using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;

namespace WorldEditor.Tests;

/// <summary>
/// Live mode without a game: a stand-in for the WorldEditorBridge plugin answers like it does.
/// </summary>
public sealed class FakeBridge : IDisposable
{
    private readonly HttpListener _listener = new();

    public int Port { get; }

    public string Token { get; } = "test-token";

    public int Status { get; set; } = 200;

    /// <summary>
    /// How many destroyed objects another mod had made again (found at their place), as the plugin tells.
    /// </summary>
    public int Refound { get; set; }

    public List<(int Destroy, int Create)> ObjectCalls { get; } = [];

    public List<(long, uint)> Destroyed { get; } = [];
    public List<(int Prefab, Vector3 Position)> Places { get; } = [];

    public List<(int X, int Z, bool Keep, bool Ground)> Resets { get; } = [];

    private uint _next = 1000;

    public FakeBridge()
    {
        Port = FreePort();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = Task.Run(Serve);
    }

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch
            {
                return;
            }
            string body = "";
            int status = ctx.Request.Headers["X-Bridge-Token"] == Token ? Status : 401;
            if (status == 200 && ctx.Request.Url!.AbsolutePath == "/status")
            {
                body = "{\"plugin\":\"0.3.0\",\"world\":\"CITest\",\"players\":0}";
            }
            else if (status == 200 && ctx.Request.Url!.AbsolutePath == "/objects")
            {
                using var r = new BinaryReader(ctx.Request.InputStream);
                int destroy = r.ReadInt32();
                for (int i = 0; i < destroy; i++)
                {
                    Destroyed.Add((r.ReadInt64(), r.ReadUInt32()));
                }
                int create = r.ReadInt32();
                var ids = new List<string>();
                for (int i = 0; i < create; i++)
                {
                    r.ReadBytes(r.ReadInt32());
                    ids.Add($"77:{_next++}");
                }
                // What each destroyed object is and where (the editor adds it after the creations).
                for (int i = 0; i < destroy; i++)
                {
                    Places.Add((r.ReadInt32(), new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle())));
                }
                lock (ObjectCalls)
                {
                    ObjectCalls.Add((destroy, create));
                }
                body = $"{{\"destroyed\":{destroy},\"missing\":0,\"refound\":{Refound},\"created\":[{string.Join(",", ids.Select(i => $"\"{i}\""))}]}}";
            }
            else if (status == 200 && ctx.Request.Url!.AbsolutePath == "/zones/reset")
            {
                using var r = new BinaryReader(ctx.Request.InputStream);
                int n = r.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    Resets.Add((r.ReadInt32(), r.ReadInt32(), r.ReadBoolean(), r.ReadBoolean()));
                }
                body = $"{{\"zones\":{n},\"destroyed\":0,\"locations\":0}}";
            }
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        }
    }

    /// <summary>
    /// A local TCP port free right now.
    /// </summary>
    public static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    /// <inheritdoc/>
    public void Dispose() => _listener.Close();
}
