using TerrainEditor.App;

namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>
    /// Sends a request with the token (and a binary body and call id when given); the answer, whatever its status, for
    /// the caller to check and dispose.
    /// </summary>
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, byte[]? body = null, string? callId = null, CancellationToken cancel = default)
    {
        using HttpRequestMessage req = new(method, path);
        if (body != null)
        {
            req.Content = new ByteArrayContent(body);
        }
        // A token the header refuses is in the error: kept out of the log.
        Log.Secret(token);
        req.Headers.Add("X-Bridge-Token", token);
        if (callId != null)
        {
            req.Headers.Add("X-Call-Id", callId);
        }
        return await _http.SendAsync(req, cancel);
    }

    /// <summary>A request body written with a binary writer (UTF-8 strings, as the plugin's ZPackage reads them).</summary>
    private static byte[] Build(Action<BinaryWriter> write)
    {
        using MemoryStream ms = new();
        using (BinaryWriter w = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            write(w);
        }
        return ms.ToArray();
    }

    /// <summary>A point as three floats (x, y, z).</summary>
    private static void Write(BinaryWriter w, System.Numerics.Vector3 v)
    {
        w.Write(v.X);
        w.Write(v.Y);
        w.Write(v.Z);
    }

    /// <summary>A GET request with the token; the caller disposes the answer.</summary>
    /// <exception cref="InvalidOperationException">The bridge answered with an error status.</exception>
    private async Task<HttpResponseMessage> Get(string path)
    {
        HttpResponseMessage res = await Send(HttpMethod.Get, path);
        if (!res.IsSuccessStatusCode)
        {
            using (res)
            {
                string body = await res.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"bridge {path}: {(int)res.StatusCode} {body}");
            }
        }
        return res;
    }

    /// <summary>A GET request's answer as text.</summary>
    /// <exception cref="InvalidOperationException">The bridge answered with an error status.</exception>
    private async Task<string> GetText(string path)
    {
        using var res = await Get(path);
        return await res.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// The whole persistent world as the running game has it (world info, zone data and every persistent object),
    /// gzip-compressed; <c>WorldSave.LoadLive</c> reads it.
    /// </summary>
    public async Task<byte[]> Snapshot()
    {
        using var res = await Get("snapshot");
        return await res.Content.ReadAsByteArrayAsync();
    }
}
