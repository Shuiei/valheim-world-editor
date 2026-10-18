using System.Text;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The relay's loop (--mcp-stdio): reads the client's messages until its input ends, and relays each one (ClaudeRelay.RelayLine).
/// </summary>
public static partial class ClaudeRelay
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
            protocol = await RelayLine(http, line, output, settings, protocol);
        }
    }
}
