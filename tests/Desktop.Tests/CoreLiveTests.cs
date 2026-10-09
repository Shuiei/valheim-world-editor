using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// Live mode without a game: a stand-in for the WorldEditorBridge plugin answers like it does.
public sealed class FakeBridge : IDisposable
{
	private readonly HttpListener _listener = new();

	public int Port { get; }

	public string Token { get; } = "test-token";

	public int Status { get; set; } = 200;

	public List<(int Destroy, int Create)> ObjectCalls { get; } = new();

	public List<(long, uint)> Destroyed { get; } = new();
	public List<(int Prefab, Vector3 Position)> Places { get; } = new();

	public List<(int X, int Z, bool Keep, bool Ground)> Resets { get; } = new();

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
				body = $"{{\"destroyed\":{destroy},\"missing\":0,\"created\":[{string.Join(",", ids.Select(i => $"\"{i}\""))}]}}";
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

public class LiveTests
{
	[Fact]
	public async Task CheckAcceptsAnAnsweringBridge()
	{
		using var bridge = new FakeBridge();
		Assert.Null(await LiveBridge.Check($"http://127.0.0.1:{bridge.Port}", bridge.Token, TimeSpan.FromSeconds(5)));
	}

	[Fact]
	public async Task CheckExplainsAWrongToken()
	{
		using var bridge = new FakeBridge();
		string? why = await LiveBridge.Check($"http://127.0.0.1:{bridge.Port}", "wrong", TimeSpan.FromSeconds(5));
		Assert.Contains("refused the token", why);
	}

	[Fact]
	public async Task CheckExplainsAGameThatDoesNotHostTheWorld()
	{
		using var bridge = new FakeBridge { Status = 409 };
		Assert.Contains("does not host the world", await LiveBridge.Check($"http://127.0.0.1:{bridge.Port}", bridge.Token, TimeSpan.FromSeconds(5)));
	}

	[Fact]
	public async Task CheckExplainsNothingListening()
	{
		int port = FakeBridge.FreePort();
		Assert.Contains("SSH tunnel", await LiveBridge.Check($"http://127.0.0.1:{port}", "x", TimeSpan.FromSeconds(5)));
	}

	[Fact]
	public async Task CheckGivesUpOnASilentServer()
	{
		// Accepts the connection, never answers.
		var silent = new TcpListener(IPAddress.Loopback, 0);
		silent.Start();
		int port = ((IPEndPoint)silent.LocalEndpoint).Port;
		var sw = System.Diagnostics.Stopwatch.StartNew();
		string? why = await LiveBridge.Check($"http://127.0.0.1:{port}", "x", TimeSpan.FromSeconds(2));
		silent.Stop();
		Assert.Contains("No answer", why);
		Assert.True(sw.Elapsed < TimeSpan.FromSeconds(6), "gives up after the timeout");
	}

	[Fact]
	public async Task CheckRejectsAnInvalidAddress()
	{
		Assert.Contains("not a valid address", await LiveBridge.Check("not a url:::", "x", TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public void SnapshotLoadsLikeTheSave()
	{
		WorldSave live = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
		Assert.True(live.IsLive);
		Assert.Equal("CITest", live.Name);
		Assert.Equal(WorldSave.Load(Fixtures.World).ObjectCount, live.ObjectCount);
	}

	[Fact]
	public async Task ApplySendsOnlyTheDifferenceAndUndoWorksAfterApplying()
	{
		using var bridge = new FakeBridge();
		WorldSave world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
		var edits = new EditStore(world);
		var sync = new LiveSync();
		var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
		var tree = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));

		// Plant one, delete one: one call, with one creation and one destruction.
		edits.AddObjects(new[] { new NewObject(-1, Fixtures.Hash("Beech1"), tree.Position + new Vector3(3, 0, 3), Vector3.Zero, 0f) });
		edits.SetDeleted(new[] { tree.Id }, true);
		Assert.Equal((1, 1), sync.Pending(edits));
		await sync.Apply(world, edits, live);
		Assert.Equal((1, 1), bridge.ObjectCalls.Single());
		Assert.Equal((0, 0), sync.Pending(edits));

		// Nothing new: nothing is sent.
		Assert.Equal("", await sync.Apply(world, edits, live));
		Assert.Single(bridge.ObjectCalls);

		// Undo both: the planted one is destroyed in the game (by its live id), the deleted one comes back.
		edits.SetDeleted(new[] { -1 }, true);
		edits.SetDeleted(new[] { tree.Id }, false);
		Assert.Equal((1, 1), sync.Pending(edits));
		await sync.Apply(world, edits, live);
		Assert.Equal((1, 1), bridge.ObjectCalls[1]);
		Assert.Contains((77L, 1000u), bridge.Destroyed);
		// With what it is and where: a mod that made it again under a new id has it there.
		Assert.Contains((Fixtures.Hash("Beech1"), tree.Position + new Vector3(3, 0, 3)), bridge.Places);
		Assert.Contains((tree.Prefab, tree.Position), bridge.Places);
		Assert.Equal((0, 0), sync.Pending(edits));
	}

	[Fact]
	public async Task ZoneResetsAreSentToTheGame()
	{
		using var bridge = new FakeBridge();
		var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
		string reply = await live.ResetZones(new[] { new ZoneReset(3, -2, true, false), new ZoneReset(-7, 9, false, true) });
		Assert.Contains("\"zones\":2", reply);
		Assert.Equal(new[] { (3, -2, true, false), (-7, 9, false, true) }, bridge.Resets);
	}
}
