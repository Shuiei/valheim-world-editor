using System.Net;
using System.Numerics;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Live mode without a game: a stand-in for the WorldEditorBridge plugin answers like it does. The objects call is in
/// FakeBridge.Objects.cs.
/// </summary>
public sealed partial class FakeBridge : IDisposable
{
    private readonly HttpListener _listener = new();
    /// <summary>Answering the requests: Dispose waits for it to end, and its errors fail the test.</summary>
    private readonly Task _serving;
    /// <summary>
    /// Set by Dispose before the listener is closed: on Windows the wait for a request can fail before the listener
    /// says it has stopped.
    /// </summary>
    private volatile bool _closing;

    public int Port { get; }

    /// <summary>Whether answering ended with an error (Dispose then throws it).</summary>
    public bool Broken => _serving.IsFaulted;

    public string Token { get; } = "test-token";

    public int Status { get; set; } = 200;

    /// <summary>Whether its status request fails (500) while the other requests are answered.</summary>
    public bool StatusFails { get; set; }

    /// <summary>
    /// How many destroyed objects another mod had made again (found at their place), as the plugin tells.
    /// </summary>
    public int Refound { get; set; }

    public List<(int Destroy, int Create)> ObjectCalls { get; } = [];

    public List<(long, uint)> Destroyed { get; } = [];
    public List<(int Prefab, Vector3 Position)> Places { get; } = [];

    public List<(int X, int Z, bool Keep, bool Ground)> Resets { get; } = [];

    /// <summary>The prefabs the zone resets asked to keep with the buildings (mods' data holders).</summary>
    public List<int> KeptWithBuildings { get; } = [];

    /// <summary>The features its status lists ("move": it takes moves).</summary>
    public List<string> Features { get; } = [];

    /// <summary>Whether it moves objects in place (else, as when a player has both places loaded, makes them again).</summary>
    public bool MoveInPlace { get; set; } = true;

    /// <summary>The moves sent: the object (ZDOID, prefab, place), its new place and turn.</summary>
    public List<(long User, uint Id, int Prefab, Vector3 From, Vector3 To, Vector3 Rotation)> Moves { get; } = [];

    /// <summary>Each move's object at its new place, in the save format, in the order of <see cref="Moves"/>.</summary>
    public List<byte[]> MoveBytes { get; } = [];

    /// <summary>Whether each move takes its object's new data in place ("update"), in the order of <see cref="Moves"/>.</summary>
    public List<bool> MoveData { get; } = [];

    /// <summary>
    /// The editor's port of the connection each request came on, in order: the same port twice is one connection
    /// used again.
    /// </summary>
    public List<int> Connections { get; } = [];

    /// <summary>This run of the plugin, as its status tells; a new one stands for the game started again.</summary>
    public string Run { get; set; } = "run1";

    /// <summary>The next objects call is done, but its answer is cut off on the way (as a dropped tunnel would).</summary>
    public bool LoseNextAnswer { get; set; }

    /// <summary>The objects calls' answers by their call id: one sent again is answered with them, not done twice.</summary>
    private readonly Dictionary<string, string> _answers = [];

    private uint _next = 1000;

    public FakeBridge()
    {
        Port = LoopbackPort.Free();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _serving = Task.Run(Serve);
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
            catch (Exception) when (_closing || !_listener.IsListening)
            {
                // Closed (Dispose): the wait for the next request ends with an error, and so does the serving.
                return;
            }
            Connections.Add(ctx.Request.RemoteEndPoint.Port);
            await Answer(ctx);
        }
    }

    /// <summary>
    /// The status the plugin answers with: its version, world, features and run.
    /// </summary>
    private string StatusBody() =>
        "{\"plugin\":\"0.3.0\",\"world\":\"CITest\",\"players\":0,\"features\":[" + string.Join(",", Features.Select(f => $"\"{f}\"")) + "],\"run\":\"" + Run + "\"}";

    /// <summary>
    /// A local TCP port free right now.
    /// </summary>
    public static int FreePort() => LoopbackPort.Free();

    /// <summary>Stops answering and waits until the answering has ended; an error in it fails the test.</summary>
    public void Dispose()
    {
        _closing = true;
        _listener.Close();
        Assert.True(_serving.Wait(TimeSpan.FromSeconds(30)), "the stand-in plugin did not stop answering");
    }
}
