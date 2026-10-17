using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The dungeons read for the view in the background: what is shown while a read is under way, and the read itself.
/// </summary>
public static partial class DungeonRooms
{
    /// <summary>
    /// For drawing (each frame): what was read last, without waiting; after an edit they are read again on
    /// a worker (chunk files, each dungeon's data), and ready is called when they are (or the read failed: logged).
    /// </summary>
    public static IReadOnlyList<Dungeon> Shown(WorldScene s, Action ready)
    {
        var now = Now(s);
        Caches.TryGetValue(s, out var c);
        if (c is { } k && k.At == now)
        {
            return k.Dungeons;
        }
        IReadOnlyList<Dungeon> shown = c is { } previous ? previous.Dungeons : [];
        if (Reading.TryAdd(s, new object()))
        {
            Task.Run(() => ReadInBackground(s, now, shown, ready));
        }
        return shown;
    }

    /// <summary>
    /// The read Shown starts on a worker: the dungeons read as the scene is at the time, then ready is called.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="now">The scene's state when the read was asked for.</param>
    /// <param name="shown">What was shown before, kept when the read fails.</param>
    /// <param name="ready">Called when the read is done, or failed.</param>
    private static void ReadInBackground(WorldScene s, (int Version, int Generation, int Count) now, IReadOnlyList<Dungeon> shown, Action ready)
    {
        try
        {
            Of(s);
        }
        catch (Exception e)
        {
            // Logged, and what was shown kept for this state: a read failing the same way is not started again
            // on every frame.
            Log.Caught(e, "reading the dungeons' rooms (what was shown is kept)");
            Caches.AddOrUpdate(s, new Cache(now, shown));
        }
        finally
        {
            Reading.Remove(s);
        }
        ready();
    }
}
