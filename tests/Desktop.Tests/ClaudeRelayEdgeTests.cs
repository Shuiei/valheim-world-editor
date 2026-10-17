using System.Diagnostics;
using System.Net;
using TerrainEditor.App;
using Xunit;
using System.Text.Json.Nodes;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The --mcp-stdio relay (ClaudeRelay) at its edges: a message that is JSON null (no message) or has no method, an
/// answer with no content type, an initialize answered without a protocol version before the one with it; and the editor's program
/// started as the relay, on its own standard input and output.
/// </summary>
public class ClaudeRelayEdgeTests
{
    /// <summary>An HTTP handler answering every request with the given function, without a network.</summary>
    private sealed class Answer(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(answer(request));
    }

    // null is no message (an error, not sent); a message with no method is answered as it comes, with no content
    // type; of initialize's answers only the one with a protocol version sets it for the next request.
    [Fact]
    public async Task OddMessagesAndAnswers()
    {
        var settings = new AppSettings { ClaudeConnect = true, ClaudeToken = "tok", ClaudePort = 5799 };
        var versions = new List<string>();
        HttpResponseMessage Reply(HttpRequestMessage req)
        {
            string body = req.Content!.ReadAsStringAsync().Result;
            if (req.Headers.TryGetValues("MCP-Protocol-Version", out var v))
            {
                versions.AddRange(v);
            }
            if (body.Contains("initialize", StringComparison.Ordinal))
            {
                const string Stream = "event: message\ndata: null\ndata: {\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":1,\"message\":\"x\"}}\n"
                    + "data: {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}\ndata: {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2025-06-18\"}}\n\n";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Stream, System.Text.Encoding.UTF8, "text/event-stream") };
            }
            if (body.Contains("tools/list", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{}}", System.Text.Encoding.UTF8, "application/json") };
            }
            // No content type at all.
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":9,\"seen\":true}")) };
        }
        var output = new StringWriter();
        const string Input = "null\n{\"jsonrpc\":\"2.0\",\"id\":9,\"result\":{}}\n{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}\n{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}\n";
        await ClaudeRelay.RunAsync(new StringReader(Input), output, () => settings, new Answer(Reply));
        string[] lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(7, lines.Length);
        Assert.Contains("\"code\":-32600", lines[0]);
        Assert.Equal("{\"jsonrpc\":\"2.0\",\"id\":9,\"seen\":true}", lines[1]);
        Assert.Equal("null", lines[2]);
        Assert.Contains("protocolVersion", lines[5]);
        Assert.Equal("{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{}}", lines[6]);
        Assert.Equal(["2025-06-18"], versions);
    }

    // The editor's program with --mcp-stdio and settings of its own (Claude not let in): a request is answered with
    // the error on its standard output, and the program ends with its input.
    [Fact]
    public async Task TheProgramRelaysOnItsStandardInputAndOutput()
    {
        string home = Path.Combine(Path.GetTempPath(), "vwe-relay-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            // The copy beside the tests, so that its coverage adds to theirs (EditorApp).
            string app = EditorApp.Path;
            var psi = new ProcessStartInfo(EditorApp.Dotnet) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add(app);
            psi.ArgumentList.Add("--mcp-stdio");
            // Its settings from an empty folder: never the user's.
            psi.Environment["XDG_DATA_HOME"] = home;
            psi.Environment["HOME"] = home;
            psi.Environment["LOCALAPPDATA"] = home;
            using var p = Process.Start(psi)!;
            var errors = p.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await p.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
            p.StandardInput.Close();
            string said = await p.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
            await p.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
            Assert.True(p.ExitCode == 0, await errors);
            Assert.Contains("does not let Claude connect", said);
            Assert.Contains("\"id\":1", said);
        }
        finally
        {
            if (Directory.Exists(home))
            {
                Directory.Delete(home, true);
            }
        }
    }

    [Fact]
    public async Task AnEventStreamsDataLinesAreWrittenOneEach()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("event: x\ndata: {\"a\":1}\n\ndata: {\"b\":2}\n", System.Text.Encoding.UTF8, "text/event-stream") };
        var output = new StringWriter();
        string? protocol = await ClaudeRelay.WriteAnswers(response, "event: x\ndata: {\"a\":1}\n\ndata: {\"b\":2}\n", "tools/list", output, null);
        Assert.Null(protocol);
        Assert.Equal("{\"a\":1}" + Environment.NewLine + "{\"b\":2}" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public async Task AnInitializeAnswersAgreedProtocolVersionIsKept()
    {
        const string body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2025-06-18\"}}";
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        Assert.Equal("2025-06-18", await ClaudeRelay.WriteAnswers(response, body, "initialize", new StringWriter(), null));
    }

    [Fact]
    public async Task AnAnswerToAnotherMethodLeavesTheProtocolVersionAsItWas()
    {
        const string body = "{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"protocolVersion\":\"2025-06-18\"}}";
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        Assert.Equal("2024-11-05", await ClaudeRelay.WriteAnswers(response, body, "tools/list", new StringWriter(), "2024-11-05"));
    }

    [Fact]
    public async Task AnEmptyAnswerWritesNothing()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("   ", System.Text.Encoding.UTF8, "application/json") };
        var output = new StringWriter();
        await ClaudeRelay.WriteAnswers(response, "   ", "tools/list", output, null);
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public async Task ASendReturnsTheAnswerWhenTheEditorGivesOne()
    {
        var (response, failure) = await ClaudeRelay.Send(new HttpClient(new Answer(_ => new HttpResponseMessage(HttpStatusCode.OK))), new HttpRequestMessage(HttpMethod.Post, "http://localhost/"));
        Assert.NotNull(response);
        Assert.Null(failure);
    }

    [Fact]
    public async Task ASendSaysTheEditorIsNotRunningWhenItCannotBeReached()
    {
        var (response, failure) = await ClaudeRelay.Send(new HttpClient(new Answer(_ => throw new HttpRequestException("refused"))), new HttpRequestMessage(HttpMethod.Post, "http://localhost/"));
        Assert.Null(response);
        Assert.Contains("is not running", failure!);
    }

    [Fact]
    public async Task ASendSaysTheEditorGaveNoAnswerInTimeWhenTheClientTimesOut()
    {
        var (response, failure) = await ClaudeRelay.Send(new HttpClient(new Answer(_ => throw new TaskCanceledException("slow", new TimeoutException()))), new HttpRequestMessage(HttpMethod.Post, "http://localhost/"));
        Assert.Null(response);
        Assert.Contains("gave no answer within", failure!);
    }

    [Fact]
    public async Task AForwardedMessageThatCannotBeReachedIsAnsweredWithItsId()
    {
        var output = new StringWriter();
        var settings = new AppSettings { ClaudeConnect = true, ClaudeToken = "tok", ClaudePort = 5799 };
        string line = "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/list\"}";
        string? protocol = await ClaudeRelay.Forward(new HttpClient(new Answer(_ => throw new HttpRequestException("refused"))), line, settings, JsonNode.Parse("3"), "tools/list", output, "2024-11-05");
        Assert.Equal("2024-11-05", protocol);
        Assert.Contains("\"id\":3", output.ToString());
        Assert.Contains("is not running", output.ToString());
    }

    [Fact]
    public async Task AForwardedMessageWithoutAnIdGetsNoErrorAnswer()
    {
        var output = new StringWriter();
        var settings = new AppSettings { ClaudeConnect = true, ClaudeToken = "tok", ClaudePort = 5799 };
        await ClaudeRelay.Forward(new HttpClient(new Answer(_ => throw new HttpRequestException("refused"))), "{\"jsonrpc\":\"2.0\",\"method\":\"x\"}", settings, null, "x", output, null);
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public async Task ALineThatIsNotJsonIsAnsweredWithAParseErrorAndTheProtocolKept()
    {
        var output = new StringWriter();
        string? protocol = await ClaudeRelay.RelayLine(new HttpClient(), "{nope", output, () => new AppSettings(), "2025-06-18");
        Assert.Equal("2025-06-18", protocol);
        Assert.Contains("-32700", output.ToString());
    }

    [Fact]
    public async Task ABatchLineIsAnsweredWithAnInvalidRequestError()
    {
        var output = new StringWriter();
        await ClaudeRelay.RelayLine(new HttpClient(), "[1]", output, () => new AppSettings(), null);
        Assert.Contains("-32600", output.ToString());
    }
}
