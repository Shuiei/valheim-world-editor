
namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>
    /// New terrain data for zones (TerrainComp.Save format), as a ZPackage: count, then x, z, byte array.
    /// </summary>
    /// <returns>The plugin's answer text.</returns>
    /// <exception cref="BridgeAnswerException">The bridge answered with an error status.</exception>
    public async Task<string> ApplyTerrain(IReadOnlyList<(int X, int Z, byte[] Data)> zones)
    {
        using var res = await Send(HttpMethod.Post, "terrain", Build(w =>
        {
            w.Write(zones.Count);
            foreach (var (x, z, data) in zones)
            {
                w.Write(x);
                w.Write(z);
                w.Write(data.Length);
                w.Write(data);
            }
        }));
        string body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new BridgeAnswerException($"bridge terrain: {(int)res.StatusCode} {body}", (int)res.StatusCode);
        }
        return body;
    }
}
