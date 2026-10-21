using System.Linq;
using System.Text;
using ValheimWorldEditor.Sdk;

namespace ExampleVehicles.WorldEditor;

/// <summary>
/// The extension's side of live mode: when the editor connects to a game where the mod's own plugin registered with
/// the bridge (sdk/samples/ExampleVehicles.Bridge), it names the game's own ships (read from the game for it: the manifest's
/// "gameComponents"), calls that plugin's "ping" route and logs the answer. A real
/// extension would ask the game for what only it knows (what the mod built in code), keep it, and ask the editor to
/// draw its shapes again (<see cref="IEditorHost.Redraw"/>).
/// </summary>
/// <param name="registry">To log through, and for the editor (<see cref="IExtensionRegistry.Host"/>).</param>
public sealed class LiveLink(IExtensionRegistry registry) : ILiveListener
{
    /// <summary>The id the mod's plugin registers under.</summary>
    public const string ModId = "ExampleVehicles";

    /// <inheritdoc/>
    public void OnConnected(ILiveGame live)
    {
        // The game's own ships, read for the extension from the player's Valheim (the manifest's "gameComponents").
        var ships = registry.Host.Game.Components("Ship").Select(c => c.Prefab);
        registry.Log("the game's ships: " + string.Join(", ", ships));
        if (!live.Mods.Contains(ModId))
        {
            registry.Log("the game has no Example Vehicles plugin registered with the bridge");
            return;
        }
        // The call goes on after this returns: a task of its own, which catches everything (an exception after an
        // await is nobody else's to catch, and would bring the editor down).
        _ = PingAsync(live);
    }

    /// <summary>Calls the plugin's "ping" and logs its answer, or why there was none.</summary>
    private async System.Threading.Tasks.Task PingAsync(ILiveGame live)
    {
        try
        {
            byte[] answer = await live.CallAsync(ModId, "ping", Encoding.UTF8.GetBytes("hello"));
            registry.Log("the game says: " + Encoding.UTF8.GetString(answer));
        }
        catch (System.Exception e)
        {
            registry.Log("the game did not answer: " + e.Message);
        }
    }

    /// <inheritdoc/>
    public void OnDisconnected() => registry.Log("no longer connected to a game");
}
