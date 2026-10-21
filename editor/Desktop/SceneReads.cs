using System.Runtime.CompilerServices;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// What the 3D view reads of an area's objects' data (a dungeon's rooms, what stands hold), kept per scene with the
/// state it was read at: read again when the edits, the live game or the object count change. For drawing, what was
/// read last is shown while a read goes on in the background.
/// </summary>
/// <typeparam name="T">What is read (a list).</typeparam>
internal sealed class SceneReads<T> where T : class
{
    /// <summary>What was read for a scene, with the state it was read at (see <see cref="Now"/>).</summary>
    private sealed record Cache((int Version, int Generation, int Count) At, T Value);

    /// <summary>The read itself, on the caller's thread.</summary>
    private readonly Func<WorldScene, T> _read;
    /// <summary>What is shown before anything was read.</summary>
    private readonly T _none;
    /// <summary>What a failed read in the background is logged as doing.</summary>
    private readonly string _doing;
    /// <summary>What was read last, per scene.</summary>
    private readonly ConditionalWeakTable<WorldScene, Cache> _caches = [];
    /// <summary>Scenes being read again for the view.</summary>
    private readonly ConditionalWeakTable<WorldScene, object> _reading = [];

    /// <summary>Reads of scenes by <paramref name="read"/>.</summary>
    /// <param name="read">The read, on the caller's thread.</param>
    /// <param name="none">What is shown before anything was read.</param>
    /// <param name="doing">What a failed read in the background is logged as doing.</param>
    public SceneReads(Func<WorldScene, T> read, T none, string doing)
    {
        _read = read;
        _none = none;
        _doing = doing;
    }

    /// <summary>
    /// The scene's edit version, its session's generation and its object count: when any changes, the scene is read
    /// again. The generation moves when the live game changes things in place (TakeGameChanges: a dungeon laid out or
    /// gone in game), which touches neither the edits nor the count.
    /// </summary>
    internal static (int Version, int Generation, int Count) Now(WorldScene s)
    {
        lock (s.Things)
        {
            return s.Session is { } session
                ? (session.Edits.Version, session.Generation, s.Things.Count)
                : (0, 0, s.Things.Count);
        }
    }

    /// <summary>The scene as it is now, read when the cache is out of date (on the caller's thread).</summary>
    public T Of(WorldScene s)
    {
        var now = Now(s);
        if (_caches.TryGetValue(s, out var c) && c.At == now)
        {
            return c.Value;
        }
        T value = _read(s);
        _caches.AddOrUpdate(s, new Cache(now, value));
        return value;
    }

    /// <summary>
    /// For drawing (each frame): what was read last, without waiting; after a change the scene is read again on a
    /// worker, and ready is called when it is (or the read failed: logged).
    /// </summary>
    public T Shown(WorldScene s, Action ready)
    {
        var now = Now(s);
        _caches.TryGetValue(s, out var c);
        if (c is { } k && k.At == now)
        {
            return k.Value;
        }
        T shown = c is { } previous ? previous.Value : _none;
        if (_reading.TryAdd(s, new object()))
        {
            Task.Run(() => ReadInBackground(s, now, shown, ready));
        }
        return shown;
    }

    /// <summary>
    /// The read Shown starts on a worker: the scene read as it is at the time, then ready is called.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="now">The scene's state when the read was asked for.</param>
    /// <param name="shown">What was shown before, kept when the read fails.</param>
    /// <param name="ready">Called when the read is done, or failed.</param>
    internal void ReadInBackground(WorldScene s, (int Version, int Generation, int Count) now, T shown, Action ready)
    {
        try
        {
            Of(s);
        }
        catch (Exception e)
        {
            // Logged, and what was shown kept for this state: a read failing the same way is not started again
            // on every frame.
            Log.Caught(e, _doing);
            _caches.AddOrUpdate(s, new Cache(now, shown));
        }
        finally
        {
            _reading.Remove(s);
        }
        ready();
    }
}
