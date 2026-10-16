using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// --mcp-stdio: for MCP clients that start a program and talk to it on its standard input and output
/// (Claude Desktop): each message read from stdin goes to the running editor's server (ClaudeServer),
/// with the token from the settings, and its answers are written to stdout, one per line. No window;
/// nothing else is written to stdout.
/// </summary>
public static class ClaudeRelay
{
    /// <summary>Relays stdin to the editor and its answers to stdout (UTF-8, no BOM) until stdin ends.</summary>
    public static void Run()
    {
        using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
        using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
        RunAsync(input, output, AppSettings.Load).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Relays each line read to the editor's server and writes its answers (JSON, or the data lines of an event
    /// stream), one per line. A line that is not JSON, or JSON that is not one message (a batch, a plain value), gets
    /// a JSON-RPC error; a message that cannot be passed on (switch off, no token, editor not running, request
    /// refused, no answer within the time allowed) gets one only when it has an id. The protocol version agreed at
    /// initialize is sent with the messages after it. Nothing ends the relay but the end of its input.
    /// </summary>
    /// <param name="input">The client's messages, one JSON-RPC message per line.</param>
    /// <param name="output">Where the answers go.</param>
    /// <param name="settings">Read again for each message (the switch may be turned on while the client runs).</param>
    /// <param name="handler">The HTTP handler (tests give their own); null: a plain one.</param>
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
            // JSON that is not one message (a batch, which MCP no longer has, null or another bare value): asking it
            // for an id or a method would throw and end the relay.
            if (message is not JsonObject)
            {
                await output.WriteLineAsync(Error(null, -32600, "Not a JSON-RPC message: one object per line (no batches)."));
                continue;
            }
            JsonNode? id = message["id"]?.DeepClone();
            string? method = message["method"] is JsonValue m && m.TryGetValue(out string? name) ? name : null;
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
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                // The client's timeout (a tool that ran too long): answered, not the end of the relay. Only that one:
                // any other cancellation is not a timeout and is not reported as one.
                if (id != null)
                {
                    await output.WriteLineAsync(Error(id, -32000, $"Valheim World Editor gave no answer within {http.Timeout.TotalMinutes} minutes."));
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
                List<string> answers = response.Content.Headers.ContentType?.MediaType == "text/event-stream"
                    ? [.. body.Split('\n').Where(l => l.StartsWith("data:", StringComparison.Ordinal)).Select(l => l[5..].Trim())]
                    : body.Trim().Length > 0 ? [body.Trim()] : [];
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

    /// <summary>A JSON-RPC error answer, one line.</summary>
    private static string Error(JsonNode? id, int code, string text) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = text } }.ToJsonString();
}
