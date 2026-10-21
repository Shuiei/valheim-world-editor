using System.Text.Json;

namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>
    /// The mods' plugins registered with the bridge (WorldEditorBridge.Api: routes to call, listeners told of changes),
    /// and the bridge's API version; none and 0 from a plugin older than 1.35.0.
    /// </summary>
    public async Task<(int Api, List<string> Mods)> Mods()
    {
        using HttpResponseMessage res = await Send(HttpMethod.Get, "mods");
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (0, []);
        }
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"bridge mods: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsByteArrayAsync());
        int api = doc.RootElement.TryGetProperty("api", out var a) ? a.GetInt32() : 0;
        var mods = doc.RootElement.TryGetProperty("mods", out var m) ? m.EnumerateArray().Select(e => e.GetString()).OfType<string>().ToList() : [];
        return (api, mods);
    }
}
