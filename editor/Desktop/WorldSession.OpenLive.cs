using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>The world of a running game or server (live).</summary>
    /// <param name="live">The connection to the game's WorldEditorBridge; the session owns it from here.</param>
    /// <param name="label">The connection's name, for <c>Label</c>.</param>
    public static async Task<WorldSession> OpenLive(LiveBridge live, string label)
    {
        WorldSession session;
        List<string> mods;
        try
        {
            await Task.Run(NeedGame);
            await LearnMods(live);
            mods = await BridgeMods(live);
            var world = await live.LoadWorld();
            // The mods' settings and data files as the game has them, through its bridge (an older one: the local folders).
            var settings = AppSettings.Load();
            world.Rules = await ExpandWorld.ResolveLive(world, settings, live);
            await ModSettings.RefreshLive(live, ModExtensions.RequestedSettingsFiles);
            // The game's world file, read through the bridge with the rules, says whether a world generation mod made the
            // world; its generator is told the mod's version the game runs.
            TakeGenerator(world, world.Generator is { } header ? await GamePluginVersion(live, header.Generator.PluginId) : null);
            WorldSave.Builder = Builders.Default(world);
            session = From(world, live, label);
        }
        catch
        {
            // No session to own the connection: it is closed here.
            live.Dispose();
            throw;
        }
        // Once the session is there to end it (Dispose): a world that failed to load leaves no game connected.
        ModExtensions.LiveConnected(new LiveGame(live, mods));
        return session;
    }

    /// <summary>
    /// The version of a plugin the game runs (GET /plugins); null when the bridge does not say (an older one, or it
    /// cannot be asked: said in the log).
    /// </summary>
    /// <param name="live">The game's bridge.</param>
    /// <param name="pluginId">The plugin's id.</param>
    private static async Task<string?> GamePluginVersion(LiveBridge live, string pluginId)
    {
        try
        {
            return (await live.Plugins())?.FirstOrDefault(p => p.Id == pluginId)?.Version;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        {
            Log.Caught(e, $"asking the game's bridge which version of {pluginId} it runs (not checked)");
            return null;
        }
    }

    /// <summary>
    /// The mods' plugins registered with the bridge (GET /mods; none from an older plugin, or when it cannot be asked:
    /// the extensions still get the game), for ModExtensions.LiveConnected once the session is open.
    /// </summary>
    private static async Task<List<string>> BridgeMods(LiveBridge live)
    {
        List<string> mods = [];
        try
        {
            (_, mods) = await live.Mods();
            if (mods.Count > 0)
            {
                Console.WriteLine($"bridge mods: {string.Join(", ", mods)}.");
            }
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // Brief: an older plugin has no list of mods.
            Log.CaughtBrief(e, "asking the game for its mods' plugins");
        }
        return mods;
    }

    /// <summary>
    /// Learns the prefabs the game's mods add (GET /catalogue) and what the game draws of them (POST /models), before
    /// the world is read so its objects get their names and models. A plugin too old to tell, or a report that cannot
    /// be read, leaves what is known as it is.
    /// </summary>
    private static async Task LearnMods(LiveBridge live)
    {
        try
        {
            string report = await live.Catalogue();
            int mods = await Task.Run(() => GameData.AddMods(report));
            Console.WriteLine($"mod catalogue: the game has {mods} mod prefabs ({ModCatalogue.Count} known in all).");
            if (mods > 0)
            {
                string models = await live.Models(ModCatalogue.Names);
                Console.WriteLine($"mod catalogue: {ModCatalogue.TakeModels(models)} models from the game.");
            }
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Caught(e, "reading the game's mod catalogue");
        }
    }
}
