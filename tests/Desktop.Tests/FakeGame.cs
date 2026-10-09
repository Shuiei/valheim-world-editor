using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Live mode without a game: a stand-in for the WorldEditorBridge plugin, answering like it does (the
// web tests' FakeBridge, with the calls the native app makes too: the world snapshot, the players, the
// ground). It records what the editor sends.
public sealed class FakeGame : IDisposable
{
	private readonly HttpListener _listener = new();
	private readonly byte[] _snapshot;

	public int Port { get; }
	public string Url => $"http://127.0.0.1:{Port}";
	public string Token { get; } = "test-token";
	public string Players { get; set; } = "[{\"name\":\"Ada\",\"x\":10,\"y\":30,\"z\":-20,\"yaw\":0},{\"name\":\"Bjorn\",\"x\":-40,\"y\":31,\"z\":5,\"yaw\":90}]";
	public int Snapshots;
	// A label of its own for each game opened (what the window shows; the history kept on disk goes by
	// it), so tests running together never share one.
	public static string Label() => "test " + Guid.NewGuid().ToString("N")[..8];
	public List<(int X, int Z)> Terrain { get; } = new();
	public List<(int Destroy, int Create)> ObjectCalls { get; } = new();
	public List<(int X, int Z, bool Keep, bool Ground)> Resets { get; } = new();
	private uint _next = 1000;
	// Answer this path with this status (an error from the game, or an older plugin without it).
	public Dictionary<string, int> Fail { get; } = new();
	// Set: ground sent to the game waits for it before the game answers (TerrainArrived says it came).
	public TaskCompletionSource? HoldTerrain { get; set; }
	public TaskCompletionSource TerrainArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	// Objects the game no longer had when asked to destroy them.
	public int Missing { get; set; }

	public FakeGame()
	{
		_snapshot = File.ReadAllBytes(Snapshot);
		Port = FreePort();
		_listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
		_listener.Start();
		_ = Task.Run(Serve);
	}

	// The fixture world as the plugin sends it.
	public static string Snapshot
	{
		get
		{
			string? root = AppContext.BaseDirectory;
			while (root != null && !File.Exists(Path.Combine(root, "tests", "fixtures", "CITest.snapshot")))
			{
				root = Path.GetDirectoryName(root);
			}
			Assert.NotNull(root);
			return Path.Combine(root, "tests", "fixtures", "CITest.snapshot");
		}
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
			byte[] body = Array.Empty<byte>();
			string type = "application/json";
			int status = ctx.Request.Headers["X-Bridge-Token"] == Token ? 200 : 401;
			string path = ctx.Request.Url!.AbsolutePath;
			try
			{
				if (status != 200)
				{
					body = Encoding.UTF8.GetBytes("{\"error\":\"token\"}");
				}
				else if (Fail.TryGetValue(path, out int failed))
				{
					status = failed;
					body = Encoding.UTF8.GetBytes("broken");
				}
				else if (path == "/status")
				{
					body = Encoding.UTF8.GetBytes("{\"plugin\":\"0.3.0\",\"world\":\"CITest\",\"players\":2}");
				}
				else if (path == "/snapshot")
				{
					Interlocked.Increment(ref Snapshots);
					body = _snapshot;
					type = "application/octet-stream";
				}
				else if (path == "/players")
				{
					body = Encoding.UTF8.GetBytes(Players);
				}
				else if (path == "/terrain")
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
					body = Encoding.UTF8.GetBytes($"{{\"zones\":{n}}}");
				}
				else if (path == "/objects")
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
						ids.Add($"\"77:{_next++}\"");
					}
					lock (ObjectCalls)
					{
						ObjectCalls.Add((destroy, create));
					}
					body = Encoding.UTF8.GetBytes($"{{\"destroyed\":{destroy - Missing},\"missing\":{Missing},\"created\":[{string.Join(",", ids)}]}}");
				}
				else if (path == "/zones/reset")
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
					body = Encoding.UTF8.GetBytes($"{{\"zones\":{n},\"destroyed\":0,\"locations\":0}}");
				}
				else
				{
					status = 404;
				}
			}
			catch (Exception ex)
			{
				status = 500;
				body = Encoding.UTF8.GetBytes(ex.Message);
			}
			ctx.Response.StatusCode = status;
			ctx.Response.ContentType = type;
			await ctx.Response.OutputStream.WriteAsync(body);
			ctx.Response.Close();
		}
	}

	public static int FreePort()
	{
		var l = new TcpListener(IPAddress.Loopback, 0);
		l.Start();
		int p = ((IPEndPoint)l.LocalEndpoint).Port;
		l.Stop();
		return p;
	}

	public void Dispose() => _listener.Close();
}
