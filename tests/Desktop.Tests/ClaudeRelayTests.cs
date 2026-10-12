using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The --mcp-stdio relay (ClaudeRelay) beyond the usual messages: a line that is not JSON, the editor refusing a
/// request, a plain JSON answer, a notification's empty answer, and no editor at all.
/// </summary>
public class ClaudeRelayTests
{
    /// <summary>An HTTP handler answering every request with the given function, without a network.</summary>
    private sealed class Answer(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(answer(request));
    }

    private static readonly AppSettings On = new() { ClaudeConnect = true, ClaudeToken = "tok", ClaudePort = 5799 };

    /// <summary>What the relay wrote for these lines, one answer per line.</summary>
    private static async Task<List<JsonElement>> Relay(string input, Func<HttpRequestMessage, HttpResponseMessage>? answer = null)
    {
        var output = new StringWriter();
        await ClaudeRelay.RunAsync(new StringReader(input), output, () => On, answer == null ? null : new Answer(answer));
        return [.. output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement)];
    }

    [Fact]
    public async Task NotJsonIsAParseError()
    {
        var answers = await Relay("this is not json\n", _ => throw new InvalidOperationException("not sent"));
        var error = Assert.Single(answers).GetProperty("error");
        Assert.Equal(-32700, error.GetProperty("code").GetInt32());
        Assert.Equal("Not JSON.", error.GetProperty("message").GetString());
    }

    // The editor's refusal is passed on for a request; a notification gets no answer either way.
    [Fact]
    public async Task ARefusalIsPassedOnForRequestsOnly()
    {
        var answers = await Relay(
            "{\"jsonrpc\":\"2.0\",\"id\":\"a\",\"method\":\"tools/list\"}\n{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}\n",
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("wrong token") });
        var only = Assert.Single(answers);
        Assert.Equal("a", only.GetProperty("id").GetString());
        Assert.Equal("The editor refused the request (401): wrong token", only.GetProperty("error").GetProperty("message").GetString());
    }

    // A plain JSON answer is written as it is; an empty one (a notification's) writes nothing.
    [Fact]
    public async Task PlainAndEmptyAnswers()
    {
        var answers = await Relay(
            "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"ping\"}\n{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}\n",
            req => req.Content!.ReadAsStringAsync().Result.Contains("ping")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("  {\"jsonrpc\":\"2.0\",\"id\":5,\"result\":{}}\n", System.Text.Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("") });
        Assert.Equal(5, Assert.Single(answers).GetProperty("id").GetInt32());
    }

    // Nothing listening on the port: the editor is not running.
    [Fact]
    public async Task NoEditorListening()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var output = new StringWriter();
        var settings = new AppSettings { ClaudeConnect = true, ClaudeToken = "tok", ClaudePort = port };
        await ClaudeRelay.RunAsync(new StringReader("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}\n"), output, () => settings);
        Assert.Contains("is not running", output.ToString());
    }
}
