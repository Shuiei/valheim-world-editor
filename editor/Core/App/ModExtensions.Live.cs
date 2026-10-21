using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The game the editor is connected to, as the running extensions are told of it, and what they ask of the editor.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>The game the editor is connected to, as extensions see it; null when it is not.</summary>
    private static volatile ILiveGame? _live;

    /// <summary>
    /// The editor connected to a game (or opened another): the running extensions' live listeners are told, and
    /// <see cref="IEditorHost.Live"/> is it from now on. Never throws.
    /// </summary>
    public static void LiveConnected(ILiveGame live)
    {
        LiveDisconnected();
        _live = live;
        foreach (var l in _state.Hooks)
        {
            foreach (var listener in l.LiveListeners.ToList())
            {
                Guard(l, () =>
                {
                    listener.OnConnected(live);
                    return true;
                }, false);
            }
        }
    }

    /// <summary>The connection ended: the listeners are told, and <see cref="IEditorHost.Live"/> is null. Never throws.</summary>
    public static void LiveDisconnected()
    {
        if (_live == null)
        {
            return;
        }
        _live = null;
        foreach (var l in _state.Hooks)
        {
            foreach (var listener in l.LiveListeners.ToList())
            {
                Guard(l, () =>
                {
                    listener.OnDisconnected();
                    return true;
                }, false);
            }
        }
    }

    /// <summary>Asks for the shapes and cuts to be made again (IEditorHost.Redraw): a generation more.</summary>
    public static void Redraw() => Interlocked.Increment(ref _generation);

    /// <summary>What an extension asks of the editor.</summary>
    private sealed class EditorHost : IEditorHost
    {
        /// <inheritdoc/>
        public ILiveGame? Live => _live;

        /// <inheritdoc/>
        public IGameData Game => ModComponents.View;

        /// <inheritdoc/>
        public string? Setting(string file, string section, string key) => ModSettings.Value(file, section, key);

        /// <inheritdoc/>
        public void Redraw() => ModExtensions.Redraw();
    }
}
