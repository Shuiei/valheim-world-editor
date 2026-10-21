using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// The history kept on disk, so it is there again when the editor is closed and the world opened later
/// (history/ of the data folder, one file per world). It is written after each save or Apply live, so its
/// steps are what the world holds. Ground points and zones are kept by world position; objects by their
/// kind and exact place (their ids change at every save), each with its full data, so one that is gone
/// when the world is opened again can be brought back by an undo or redo, chest contents and all.
/// </summary>
public static partial class HistoryFile
{
    /// <summary>The first four bytes of the file's (gzipped) contents ("VWEH").</summary>
    private const int Magic = 0x48455756; // "VWEH"
    /// <summary>The file layout's version (2: with mods' hidden objects); 1 is read too, a file of another version is not.</summary>
    private const int Version = 2;

    /// <summary>Where the history files are: history/ of the data folder.</summary>
    public static string Folder => Path.Combine(AppSettings.DataDir, "history");

    /// <summary>A world on this computer by its folder; a live one by where it runs, its name and seed.</summary>
    public static string KeyOf(WorldSession w) => w.IsLive
        ? $"live\n{w.Label}\n{w.World.Name}\n{w.World.Seed}"
        : $"file\n{Path.GetFullPath(w.World.Directory)}";

    /// <summary>
    /// The history file of a world key (<c>KeyOf</c>): a short hash of it, so any key makes a safe file name.
    /// </summary>
    public static string PathOf(string key)
    {
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant();
        return Path.Combine(Folder, hash + ".history");
    }

    /// <summary>
    /// What a world on disk looks like: its latest save (index file name, time and size). A game that
    /// saved the world since makes another. Null for a live world (it changes all the time).
    /// </summary>
    public static string? Stamp(WorldSession w)
    {
        if (w.IsLive || !Directory.Exists(w.World.Directory))
        {
            return null;
        }
        // The newest save; none left (removed while the world is open): no stamp.
        string? latest = Directory.GetFiles(w.World.Directory, "_main.*.chunks")
            .Select(f => (File: f, Parts: Path.GetFileName(f).Split('.')))
            .Where(p => p.Parts.Length == 3 && int.TryParse(p.Parts[1], out _))
            .OrderBy(p => int.Parse(p.Parts[1], System.Globalization.CultureInfo.InvariantCulture))
            .Select(p => p.File)
            .LastOrDefault();
        if (latest == null)
        {
            return null;
        }
        var info = new FileInfo(latest);
        return $"{info.Name}|{info.LastWriteTimeUtc.Ticks}|{info.Length}";
    }

    /// <summary>
    /// Removes the world's history file, after the writes queued for it; a file that cannot be removed is left as it
    /// is.
    /// </summary>
    public static void Delete(WorldSession w)
    {
        Flush();
        Remove(PathOf(KeyOf(w)));
    }

    /// <summary>Removes a history file; one that cannot be removed is left as it is, and that is logged.</summary>
    private static void Remove(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.CaughtBrief(e, "removing a history file (left as it is)");
        }
    }

    // ---- Writing.

    /// <summary>
    /// An object one of the steps needs, as written: what it is, where, and its saved data (null when it could not be
    /// read).
    /// </summary>
    private sealed record KeptThing(int Prefab, bool Piece, Vector3 Position, Vector3 Rotation, float Scale, byte[]? Bytes);

    /// <summary>
    /// One step as written: its fields at the time it was taken (its flags change later), with its points and zones
    /// already in world positions' frame and its objects by id (those not found are left out when written).
    /// </summary>
    private sealed record KeptStep(string Label, long Ticks, bool Removed, bool Applied, int RevertOf, EditSession.Frame Frame, int[] Points,
        Ground.State Before, Ground.State After, (int X, int Z)[] Zones, (int Id, bool Before, bool After)[] Things,
        (ZoneReset Reset, bool Before, bool After)[] Resets, (int Id, bool Before, bool After)[] Hidden);

    /// <summary>
    /// What finding the steps' objects and reading their data needs, taken with the steps: the world, held so a live
    /// merge leaves it as it is until they are read (<see cref="WorldSave.Hold"/>), and the edits' objects and the
    /// builder as they were (the edits go on changing on the window's thread).
    /// </summary>
    /// <param name="World">The world, held.</param>
    /// <param name="Held">The world's hold, let go once the objects are read.</param>
    /// <param name="Ids">Every object the steps need, once.</param>
    /// <param name="Deleted">The saved objects deleted then.</param>
    /// <param name="Added">The objects added then and not deleted again.</param>
    /// <param name="AddedById">The steps' added objects (deleted again or not) by id.</param>
    /// <param name="Builder">The builder chosen then.</param>
    private sealed record Sources(WorldSave World, IDisposable Held, List<int> Ids, HashSet<int> Deleted, List<NewObject> Added,
        Dictionary<int, NewObject> AddedById, long Builder);

    /// <summary>
    /// Everything a history file holds, taken from the world and the steps at one time, so it can be written later
    /// on another thread. No steps: the file is removed instead (and nothing is held).
    /// </summary>
    private sealed record Snapshot(string Path, string Key, string? Stamp, long SavedAt, Sources? Objects, List<KeptStep> Steps, int UndoCount, int RedoCount)
    {
        /// <summary>Lets the world go (again: only the first time counts).</summary>
        public void Release() => Objects?.Held.Dispose();
    }

    /// <summary>
    /// Writes the world's undo and redo steps (replacing the file in one move, so a crash leaves the old one);
    /// with no steps the file is removed. Waits for the writes <see cref="WriteLater"/> has queued first.
    /// </summary>
    public static void Write(WorldSession w, EditSession.Kept k)
    {
        Flush();
        var snapshot = Take(w, k);
        try
        {
            Save(snapshot);
        }
        finally
        {
            snapshot.Release();
        }
    }
}
