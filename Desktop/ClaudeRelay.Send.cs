using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The relay's calls to the editor (ClaudeRelay.RelayLine): the request sent, and the answers written out.
/// </summary>
public static partial class ClaudeRelay
{
    /// <summary>
    /// Sends a message to the editor's server.
    /// </summary>
    /// <param name="http">The client to send with.</param>
    /// <param name="request">The request.</param>
    /// <returns>The answer, or, when the editor could not be reached or gave none in time, why (Failure) for the error.</returns>
    internal static async Task<(HttpResponseMessage? Response, string? Failure)> Send(HttpClient http, HttpRequestMessage request)
    {
        try
        {
            return (await http.SendAsync(request), null);
        }
        catch (HttpRequestException ex)
        {
            Log.CaughtBrief(ex, "passing a message from Claude to the editor");
            return (null, "Valheim World Editor is not running (or not on this port): start it, with Settings > Claude > Allow Claude to connect on.");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            Log.CaughtBrief(ex, "waiting for the editor's answer to Claude");
            // The client's timeout (a tool that ran too long): answered, not the end of the relay. Only that one:
            // any other cancellation is not a timeout and is not reported as one.
            return (null, $"Valheim World Editor gave no answer within {http.Timeout.TotalMinutes} minutes.");
        }
    }

    /// <summary>
    /// Passes a message to the editor's server and writes its answers, or the error for the client when the editor
    /// refuses it or cannot be reached.
    /// </summary>
    /// <param name="http">The client to send with.</param>
    /// <param name="line">The message, as read.</param>
    /// <param name="s">The settings: the port and the token.</param>
    /// <param name="id">The message's id (null: none, no error is written for it).</param>
    /// <param name="method">The message's method (null: none).</param>
    /// <param name="output">Where the answers go.</param>
    /// <param name="protocol">The protocol version agreed so far (null: none).</param>
    /// <returns>The protocol version agreed so far, after these answers.</returns>
    internal static async Task<string?> Forward(HttpClient http, string line, AppSettings s, JsonNode? id, string? method, TextWriter output, string? protocol)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ClaudeServer.Url(s.ClaudePort)) { Content = new StringContent(line, Encoding.UTF8, "application/json") };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ClaudeToken);
        if (protocol != null)
        {
            request.Headers.Add("MCP-Protocol-Version", protocol);
        }
        var (response, failure) = await Send(http, request);
        if (response == null)
        {
            if (id != null)
            {
                await output.WriteLineAsync(Error(id, -32000, failure!));
            }
            return protocol;
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
                return protocol;
            }
            return await WriteAnswers(response, body, method, output, protocol);
        }
    }
}
