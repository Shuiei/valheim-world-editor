namespace TerrainEditor.App;

/// <summary>
/// The start page's view of the player's setup: whether a bridge runs, and what to do next when none does.
/// </summary>
public static partial class LocalGame
{
    /// <summary>
    /// For the start page: what the player has, and what to do next. An anonymous object (sent as JSON)
    /// whose state is "running", "installed", "plugin-not-started", "no-plugin" or "no-bepinex", with the
    /// world, players, where the bridge is and the Valheim folder.
    /// </summary>
    public static async Task<object> Status(AppSettings settings)
    {
        List<Bridge> bridges = FindBridges(settings, out bool bepInEx, out bool plugin);
        Running? running = await FindRunning(bridges);
        string state = running != null ? "running" : bridges.Count > 0 ? "installed" : plugin ? "plugin-not-started" : bepInEx ? "no-plugin" : "no-bepinex";
        return new
        {
            state,
            world = running?.World,
            players = running?.Players,
            where = running?.Bridge.Where ?? bridges.FirstOrDefault()?.Where,
            valheim = GameLook.FindValheim(settings.ValheimPath),
        };
    }
}
