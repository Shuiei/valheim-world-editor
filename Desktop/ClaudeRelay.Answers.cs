using System.Text.Json.Nodes;

namespace TerrainEditor.Desktop;

/// <summary>
/// The editor's answers to a relayed message, written out to the client (ClaudeRelay.Forward).
/// </summary>
public static partial class ClaudeRelay
{
    /// <summary>
    /// Writes the answers the editor gave to a message: the data lines of an event stream, else the body, one line
    /// each; the protocol version an initialize answer agrees to is kept for the messages after it.
    /// </summary>
    /// <param name="response">The editor's answer.</param>
    /// <param name="body">The answer's body, read.</param>
    /// <param name="method">The message's method (null: none).</param>
    /// <param name="output">Where the answers go.</param>
    /// <param name="protocol">The protocol version agreed so far (null: none).</param>
    /// <returns>The protocol version agreed so far, after these answers.</returns>
    internal static async Task<string?> WriteAnswers(HttpResponseMessage response, string body, string? method, TextWriter output, string? protocol)
    {
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
        return protocol;
    }

    /// <summary>A JSON-RPC error answer, one line.</summary>
    private static string Error(JsonNode? id, int code, string text) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = text } }.ToJsonString();
}
