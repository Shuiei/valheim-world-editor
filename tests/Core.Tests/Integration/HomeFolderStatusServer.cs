using System.Net;
using System.Text;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A local HTTP server giving every request the same status code and body, on a free port, once started.
/// </summary>
internal sealed class HomeFolderStatusServer(int code, string body) : IDisposable
{
    private readonly HttpListener _l = new();
    /// <summary>Answering the requests: Dispose waits for it to end, and its errors fail the test.</summary>
    private Task _serving = Task.CompletedTask;
    public int Port { get; } = FakeGame.FreePort();

    /// <summary>Whether answering ended with an error (Dispose then throws it).</summary>
    public bool Broken => _serving.IsFaulted;

    public HomeFolderStatusServer Start()
    {
        _l.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _l.Start();
        _serving = Task.Run(async () =>
        {
            while (true)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _l.GetContextAsync();
                }
                catch (Exception) when (!_l.IsListening)
                {
                    // Closed (Dispose): the wait for the next request ends with an error, and so does the serving.
                    return;
                }
                ctx.Response.StatusCode = code;
                await ctx.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(body));
                ctx.Response.Close();
            }
        });
        return this;
    }

    /// <summary>Stops answering and waits until the answering has ended; an error in it fails the test.</summary>
    public void Dispose()
    {
        _l.Close();
        Assert.True(_serving.Wait(TimeSpan.FromSeconds(30)), "the status server did not stop answering");
    }
}
