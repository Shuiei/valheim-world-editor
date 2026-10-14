using System.Linq;
using System.Text;
using ValheimWorldEditor.Sdk;

namespace ExampleVehicles.WorldEditor;

/// <summary>
/// The extension's side of live mode: when the editor connects to a game where the mod's own plugin registered with
/// the bridge (samples/ExampleVehicles.Bridge), it calls that plugin's "ping" route and logs the answer. A real
/// extension would ask the game for what only it knows (what the mod built in code), keep it, and ask the editor to
/// draw its shapes again (<see cref="IEditorHost.Redraw"/>).
/// </summary>
/// <param name="registry">To log through, and for the editor (<see cref="IExtensionRegistry.Host"/>).</param>
public sealed class LiveLink(IExtensionRegistry registry) : ILiveListener
{
    /// <summary>The id the mod's plugin registers under.</summary>
    public const string ModId = "ExampleVehicles";

    /// <inheritdoc/>
    public async void OnConnected(ILiveGame live)
    {
        if (!live.Mods.Contains(ModId))
        {
            registry.Log("the game has no Example Vehicles plugin registered with the bridge");
            return;
        }
        try
        {
            byte[] answer = await live.CallAsync(ModId, "ping", Encoding.UTF8.GetBytes("hello"));
            registry.Log("the game says: " + Encoding.UTF8.GetString(answer));
        }
        catch (System.Exception e) when (e is System.InvalidOperationException or System.Net.Http.HttpRequestException or System.Threading.Tasks.TaskCanceledException)
        {
            registry.Log("the game did not answer: " + e.Message);
        }
    }

    /// <inheritdoc/>
    public void OnDisconnected() => registry.Log("no longer connected to a game");
}
