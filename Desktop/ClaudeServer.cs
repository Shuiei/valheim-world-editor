using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Claude's connection (Settings: Allow Claude to connect): an MCP server inside the editor
// (ClaudeTools), so that Claude (Claude Code, Claude Desktop) can look at the open world and change it
// as the user would. Only on 127.0.0.1, only for callers that send the token (which also keeps web
// pages out: they cannot send it, nor a Host other than this computer's address). What Claude changes
// stays pending: it never saves, applies live or saves a blueprint.
public sealed class ClaudeServer
{
	private static ClaudeServer? _running;
	private static readonly SemaphoreSlim Gate = new(1, 1);

	private readonly WebApplication _app;

	public int Port { get; }

	private ClaudeServer(WebApplication app, int port)
	{
		_app = app;
		Port = port;
	}

	// The address Claude connects to.
	public static string Url(int port) => $"http://127.0.0.1:{port}/mcp";

	// What to type to connect Claude Code to the editor.
	public static string ClaudeCodeCommand(int port, string token) =>
		$"claude mcp add --transport http valheim-editor {Url(port)} --header \"Authorization: Bearer {token}\"";

	// A new secret, for the settings.
	public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

	// "Listening on …", the problem when it could not start, or null when off.
	public static string? Status { get; private set; }

	// Started or stopped as the settings say (again when they changed). The problem, or null.
	public static async Task<string?> Apply(AppSettings settings, MainWindow window)
	{
		await Gate.WaitAsync();
		try
		{
			if (_running != null)
			{
				await _running._app.StopAsync();
				await _running._app.DisposeAsync();
				_running = null;
				Status = null;
			}
			if (!settings.ClaudeConnect)
			{
				return null;
			}
			if (string.IsNullOrEmpty(settings.ClaudeToken))
			{
				settings.ClaudeToken = NewToken();
				settings.Save();
			}
			try
			{
				_running = await Start(settings.ClaudePort, settings.ClaudeToken!, new ClaudeTools(window));
				Status = $"Listening on {Url(settings.ClaudePort)}";
				Console.WriteLine($"claude: {Status}");
				return null;
			}
			catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or InvalidOperationException)
			{
				Status = $"Could not listen on port {settings.ClaudePort}: {e.Message}";
				Console.WriteLine($"claude: {Status}");
				return Status;
			}
		}
		finally
		{
			Gate.Release();
		}
	}

	// The server on 127.0.0.1:port with these tools (tests start their own).
	internal static async Task<ClaudeServer> Start(int port, string token, ClaudeTools tools)
	{
		var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
		builder.Logging.ClearProviders();
		builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, port));
		builder.Services.AddMcpServer(o => o.ServerInfo = new() { Name = "valheim-world-editor", Version = BuildInfo.Version })
			.WithHttpTransport(o => o.Stateless = true)
			.WithTools(tools);
		var app = builder.Build();
		byte[] expected = Encoding.UTF8.GetBytes("Bearer " + token);
		app.Use(async (ctx, next) =>
		{
			// This computer's address only (a web page reaching it under another name is refused), and
			// the token.
			string host = ctx.Request.Host.Host;
			if (host is not ("127.0.0.1" or "localhost"))
			{
				ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
				return;
			}
			byte[] given = Encoding.UTF8.GetBytes(ctx.Request.Headers.Authorization.ToString());
			if (!CryptographicOperations.FixedTimeEquals(given, expected))
			{
				ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
				await ctx.Response.WriteAsync("The editor's token is missing or wrong: copy it again from the editor's Settings.");
				return;
			}
			await next();
		});
		app.MapMcp("/mcp");
		await app.StartAsync();
		return new ClaudeServer(app, port);
	}

	internal async Task Stop()
	{
		await _app.StopAsync();
		await _app.DisposeAsync();
	}
}
