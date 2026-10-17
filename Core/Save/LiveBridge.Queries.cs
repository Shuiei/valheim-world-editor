
namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>The connected players and their positions, as the plugin's JSON array.</summary>
    public Task<string> Players() => GetText("players");

    /// <summary>
    /// Every prefab the running game knows, mods' too, as the plugin's JSON (ModCatalogue reads it); a plugin older
    /// than 1.26.0 answers 404.
    /// </summary>
    public Task<string> Catalogue() => GetText("catalogue");

    /// <summary>
    /// What the game draws of these prefabs (mod prefabs: their meshes', materials' names and places), as the plugin's
    /// JSON (ModCatalogue.TakeModels reads it); a plugin older than 1.27.0 answers 404.
    /// </summary>
    public async Task<string> Models(IEnumerable<string> prefabs)
    {
        using var res = await Post("models", System.Text.Encoding.UTF8.GetBytes(string.Join('\n', prefabs)));
        return await res.Content.ReadAsStringAsync();
    }

    /// <summary>Plugin and world info, as the plugin's JSON.</summary>
    public Task<string> Status() => GetText("status");
}
