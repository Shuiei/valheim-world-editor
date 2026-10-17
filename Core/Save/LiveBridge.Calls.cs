
namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>
    /// Calls a mod's route in the game (POST /mods/{modId}/{name}, answered by the mod's plugin) and gives back its
    /// answer's bytes. 404 means no such mod or route (the plugin's answer says which), not an old plugin: the caller
    /// asked <see cref="Mods"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The bridge answered with an error status (its message given).</exception>
    public async Task<byte[]> Call(string modId, string name, byte[] body, CancellationToken cancel = default)
    {
        using HttpResponseMessage res = await Send(HttpMethod.Post, $"mods/{Uri.EscapeDataString(modId)}/{Uri.EscapeDataString(name)}", body, cancel: cancel);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"bridge mods/{modId}/{name}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync(cancel)}");
        }
        return await res.Content.ReadAsByteArrayAsync(cancel);
    }
}
