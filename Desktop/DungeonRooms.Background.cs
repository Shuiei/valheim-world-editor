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
    public static IReadOnlyList<Dungeon> Shown(WorldScene s, Action ready) => Reads.Shown(s, ready);

    /// <summary>
    /// The read Shown starts on a worker: the dungeons read as the scene is at the time, then ready is called.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="now">The scene's state when the read was asked for.</param>
    /// <param name="shown">What was shown before, kept when the read fails.</param>
    /// <param name="ready">Called when the read is done, or failed.</param>
    internal static void ReadInBackground(WorldScene s, (int Version, int Generation, int Count) now, IReadOnlyList<Dungeon> shown, Action ready) => Reads.ReadInBackground(s, now, shown, ready);
}
