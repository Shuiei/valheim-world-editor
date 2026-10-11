using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// --mcp-stdio: for MCP clients that start a program and talk to it on its standard input and output
// (Claude Desktop): each message read from stdin goes to the running editor's server (ClaudeServer),
// with the token from the settings, and its answers are written to stdout, one per line. No window;
// nothing else is written to stdout.
public static class ClaudeRelay
{
	public static void Run()
	{
		using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
		using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
		RunAsync(input, output, AppSettings.Load).GetAwaiter().GetResult();
	}

	// settings: read again for each message (the switch may be turned on while the client runs).
	internal static async Task RunAsync(TextReader input, TextWriter output, Func<AppSettings> settings, HttpMessageHandler? handler = null)
	{
		using var http = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromMinutes(5) };
		string? protocol = null;
		while (await input.ReadLineAsync() is string line)
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}
			JsonNode? message;
			try
			{
				message = JsonNode.Parse(line);
			}
			catch (System.Text.Json.JsonException)
			{
				await output.WriteLineAsync(Error(null, -32700, "Not JSON."));
				continue;
			}
			JsonNode? id = message?["id"]?.DeepClone();
			string? method = message?["method"]?.GetValue<string>();
			var s = settings();
			if (!s.ClaudeConnect || string.IsNullOrEmpty(s.ClaudeToken))
			{
				if (id != null)
				{
					await output.WriteLineAsync(Error(id, -32000, "Valheim World Editor does not let Claude connect: in the editor, Settings > Claude > Allow Claude to connect."));
				}
				continue;
			}
			using var request = new HttpRequestMessage(HttpMethod.Post, ClaudeServer.Url(s.ClaudePort)) { Content = new StringContent(line, Encoding.UTF8, "application/json") };
			request.Headers.Accept.ParseAdd("application/json");
			request.Headers.Accept.ParseAdd("text/event-stream");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ClaudeToken);
			if (protocol != null)
			{
				request.Headers.Add("MCP-Protocol-Version", protocol);
			}
			HttpResponseMessage response;
			try
			{
				response = await http.SendAsync(request);
			}
			catch (HttpRequestException)
			{
				if (id != null)
				{
					await output.WriteLineAsync(Error(id, -32000, "Valheim World Editor is not running (or not on this port): start it, with Settings > Claude > Allow Claude to connect on."));
				}
				continue;
			}
			using (response)
			{
				string body = await response.Content.ReadAsStringAsync();
				if (!response.IsSuccessStatusCode)
				{
					if (id != null)
					{
						await output.WriteLineAsync(Error(id, -32000, $"The editor refused the request ({(int)response.StatusCode}): {body}"));
					}
					continue;
				}
				var answers = response.Content.Headers.ContentType?.MediaType == "text/event-stream"
					? body.Split('\n').Where(l => l.StartsWith("data:", StringComparison.Ordinal)).Select(l => l[5..].Trim()).ToList()
					: body.Trim().Length > 0 ? new List<string> { body.Trim() } : new List<string>();
				foreach (string a in answers.Where(a => a.Length > 0))
				{
					if (method == "initialize" && JsonNode.Parse(a)?["result"]?["protocolVersion"]?.GetValue<string>() is string v)
					{
						protocol = v;
					}
					await output.WriteLineAsync(a);
				}
			}
		}
	}

	private static string Error(JsonNode? id, int code, string text) =>
		new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = text } }.ToJsonString();
}
