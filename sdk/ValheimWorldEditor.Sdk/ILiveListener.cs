namespace ValheimWorldEditor.Sdk;

/// <summary>Told when the editor connects to a game and when that ends (see <see cref="IExtensionRegistry.AddLiveListener"/>).</summary>
public interface ILiveListener
{
    /// <summary>The editor is connected to a game: its mods' routes can be called from now on.</summary>
    /// <param name="live">The game.</param>
    void OnConnected(ILiveGame live);

    /// <summary>The connection ended (the world was closed, or another opened): the game given before is no longer usable.</summary>
    void OnDisconnected();
}
