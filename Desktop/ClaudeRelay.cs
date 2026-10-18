using System.Text.Json.Nodes;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The relay's handling of one line (--mcp-stdio, see ClaudeRelay.Run): what is answered here, and what is passed on.
/// </summary>
public static partial class ClaudeRelay
{
    /// <summary>
    /// Relays one line (see <see cref="RunAsync"/>): answers it itself when it is not one message, or cannot be passed
    /// on, else passes it to the editor and writes its answers.
    /// </summary>
    /// <param name="http">The client the editor is called with.</param>
    /// <param name="line">The line read.</param>
    /// <param name="output">Where the answers go.</param>
    /// <param name="settings">The settings, read for this message.</param>
    /// <param name="protocol">The protocol version agreed so far (null: none).</param>
    /// <returns>The protocol version agreed so far, after this line.</returns>
    internal static async Task<string?> RelayLine(HttpClient http, string line, TextWriter output, Func<AppSettings> settings, string? protocol)
    {
        JsonNode? message;
        try
        {
            message = JsonNode.Parse(line);
        }
        catch (System.Text.Json.JsonException ex)
        {
            Log.CaughtBrief(ex, "reading a message from Claude");
            await output.WriteLineAsync(Error(null, -32700, "Not JSON."));
            return protocol;
        }
        // JSON that is not one message (a batch, which MCP no longer has, null or another bare value): asking it
        // for an id or a method would throw and end the relay.
        if (message is not JsonObject)
        {
            await output.WriteLineAsync(Error(null, -32600, "Not a JSON-RPC message: one object per line (no batches)."));
            return protocol;
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
            return protocol;
        }
        return await Forward(http, line, s, id, method, output, protocol);
    }
}
