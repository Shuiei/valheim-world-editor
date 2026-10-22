using System.Globalization;
using System.Net;
using System.Text.Json;

namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>The first WorldEditorBridge that serves the game's files (GET /plugins, /config, /world/meta).</summary>
    public const string FilesVersion = "1.54.0";

    /// <summary>A plugin BepInEx loaded in the game, as GET /plugins lists it.</summary>
    /// <param name="Id">The plugin's GUID.</param>
    /// <param name="Name">Its name.</param>
    /// <param name="Version">Its version, as the plugin declares it.</param>
    public sealed record Plugin(string Id, string Name, string Version);

    /// <summary>An entry of a folder under the game's BepInEx/config, as GET /config lists it.</summary>
    /// <param name="Name">The file's or folder's name.</param>
    /// <param name="Folder">A folder.</param>
    /// <param name="Size">A file's size in bytes (0 for a folder).</param>
    /// <param name="Modified">When it was last written (UTC).</param>
    public sealed record ConfigEntry(string Name, bool Folder, long Size, DateTime Modified);

    /// <summary>
    /// The plugins BepInEx loaded in the game, with their versions (a plugin of 1.54.0 or newer); null from an older
    /// plugin (404).
    /// </summary>
    /// <exception cref="InvalidOperationException">The bridge answered with an error status.</exception>
    /// <exception cref="JsonException">The answer is not the plugin's JSON.</exception>
    public async Task<List<Plugin>?> Plugins()
    {
        using var res = await GetOrNone("plugins");
        if (res == null)
        {
            return null;
        }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsByteArrayAsync());
        List<Plugin> plugins = [];
        if (doc.RootElement.TryGetProperty("plugins", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in list.EnumerateArray())
            {
                plugins.Add(new Plugin(Text(p, "guid"), Text(p, "name"), Text(p, "version")));
            }
        }
        return plugins;
    }

    /// <summary>
    /// A file under the game's BepInEx/config, by its path there ("/" between parts), as its bytes; null when there is
    /// no such file (404; a folder of that name is listed by <see cref="ConfigFolder"/>), or the plugin is older than
    /// 1.54.0.
    /// </summary>
    /// <exception cref="InvalidOperationException">The bridge refused the path (out of the folder, over 4 MB) or answered with an error status.</exception>
    public async Task<byte[]?> ConfigFile(string path)
    {
        using var res = await GetOrNone("config?path=" + Uri.EscapeDataString(path));
        if (res == null || res.Content.Headers.ContentType?.MediaType == "application/json")
        {
            return null;
        }
        return await res.Content.ReadAsByteArrayAsync();
    }

    /// <summary>
    /// The entries of a folder under the game's BepInEx/config, by its path there ("" for the folder itself); null
    /// when there is no such folder (404; a file of that name is read by <see cref="ConfigFile"/>), or the plugin is
    /// older than 1.54.0.
    /// </summary>
    /// <exception cref="InvalidOperationException">The bridge refused the path (out of the folder) or answered with an error status.</exception>
    /// <exception cref="JsonException">The answer is not the plugin's JSON.</exception>
    public async Task<List<ConfigEntry>?> ConfigFolder(string path)
    {
        using var res = await GetOrNone("config?path=" + Uri.EscapeDataString(path));
        if (res == null || res.Content.Headers.ContentType?.MediaType != "application/json")
        {
            return null;
        }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsByteArrayAsync());
        List<ConfigEntry> entries = [];
        if (doc.RootElement.TryGetProperty("entries", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in list.EnumerateArray())
            {
                bool folder = e.TryGetProperty("folder", out var f) && f.ValueKind == JsonValueKind.True;
                long size = e.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0;
                var modified = DateTime.TryParse(Text(e, "modified"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when) ? when : DateTime.MinValue;
                entries.Add(new ConfigEntry(Text(e, "name"), folder, size, modified));
            }
        }
        return entries;
    }

    /// <summary>
    /// The world's meta file as it lies on the game's disk (its starting global keys as written there, which a mod may
    /// take out of the loaded list); null when the game has no world file, or the plugin is older than 1.54.0 (404).
    /// </summary>
    /// <exception cref="InvalidOperationException">The bridge answered with an error status.</exception>
    public async Task<byte[]?> WorldMeta()
    {
        using var res = await GetOrNone("world/meta");
        return res == null ? null : await res.Content.ReadAsByteArrayAsync();
    }

    /// <summary>A GET request with the token; null for 404 (the caller disposes any other answer).</summary>
    /// <exception cref="InvalidOperationException">The bridge answered with another error status.</exception>
    private async Task<HttpResponseMessage?> GetOrNone(string path)
    {
        HttpResponseMessage res = await Send(HttpMethod.Get, path);
        if (res.StatusCode == HttpStatusCode.NotFound)
        {
            res.Dispose();
            return null;
        }
        if (!res.IsSuccessStatusCode)
        {
            using (res)
            {
                throw new InvalidOperationException($"bridge {path}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
            }
        }
        return res;
    }

    /// <summary>A string property of a JSON object, "" when absent or not a string.</summary>
    private static string Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
