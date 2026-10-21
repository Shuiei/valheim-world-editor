using System.Diagnostics.CodeAnalysis;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using WorldEditorBridge.Api;

namespace ExampleVehicles.Bridge;

/// <summary>
/// Example Vehicles' plugin, as far as the editor is concerned: when the WorldEditorBridge is installed too, it registers
/// with it (through <see cref="BridgeLink"/>, a class of its own so that this plugin loads without the bridge), to be
/// told what the editor applied and to answer its editor extension's calls.
/// </summary>
[BepInPlugin(PluginId, "ExampleVehicles", "1.0.0")]
[BepInDependency(BridgeApi.BridgeGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class ExampleVehiclesBridgePlugin : BaseUnityPlugin
{
    /// <summary>This plugin's id, and the id it registers with the bridge under.</summary>
    public const string PluginId = "ExampleVehicles";

    /// <summary>Registers with the bridge, when it is there.</summary>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Unity calls it.")]
    private void Awake()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Register(Logger);
        }
    }

    /// <summary>Takes the registration away.</summary>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Unity calls it.")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Unity calls it on the instance.")]
    private void OnDestroy()
    {
        if (Chainloader.PluginInfos.ContainsKey(BridgeApi.BridgeGuid))
        {
            BridgeLink.Unregister();
        }
    }
}

/// <summary>
/// The part that touches the bridge's API: in a class of its own, it is only loaded once the bridge is known to be
/// there (a plugin without it would fail to load otherwise).
/// </summary>
internal static class BridgeLink
{
    /// <summary>Registers the routes and the listener.</summary>
    public static void Register(BepInEx.Logging.ManualLogSource log)
    {
        BridgeApi.AddRoutes(ExampleVehiclesBridgePlugin.PluginId, new Routes());
        BridgeApi.AddListener(ExampleVehiclesBridgePlugin.PluginId, new Listener(log));
        log.LogInfo("ExampleVehicles: registered with the WorldEditorBridge");
    }

    /// <summary>Takes them away.</summary>
    public static void Unregister() => BridgeApi.Remove(ExampleVehiclesBridgePlugin.PluginId);

    /// <summary>
    /// The routes the editor extension calls: "ping" answers "pong" and what it was sent, "players" how many players
    /// are in the game (what only the running game knows).
    /// </summary>
    private sealed class Routes : IBridgeRoutes
    {
        /// <inheritdoc/>
        public byte[] Handle(string name, byte[] body) => name switch
        {
            "ping" => Encoding.UTF8.GetBytes("pong " + Encoding.UTF8.GetString(body)),
            "players" => Encoding.UTF8.GetBytes(ZNet.instance.GetPlayerList().Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            _ => null,
        };
    }

    /// <summary>
    /// Told of the editor's changes: a real mod would put its vehicles together again here (reattach the parts the
    /// editor moved to their roots); this one says what it was told.
    /// </summary>
    private sealed class Listener(BepInEx.Logging.ManualLogSource log) : IBridgeListener
    {
        /// <inheritdoc/>
        public void OnApplied(AppliedChanges changes) =>
            log.LogInfo($"ExampleVehicles: the editor made {changes.Created.Count} object(s), destroyed {changes.Destroyed.Count}, rewrote the ground of {changes.TerrainZones.Count} zone(s), reset {changes.ResetZones.Count}");
    }
}
