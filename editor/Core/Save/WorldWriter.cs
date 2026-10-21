using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

/// <summary>
/// Writes terrain edits back into a chunked world save, following ZNet.SaveWorldThread:
/// <list type="number">
/// <item><description>
/// changed chunk files are written under a new version number; untouched objects are copied byte for byte, terrain
/// objects get new TCData, new terrain objects are appended
/// </description></item>
/// <item><description>
/// a new save number (the highest there + 1): _main.&lt;n&gt;.chunks (updated index), .db2 and .fwl2
/// </description></item>
/// <item><description>
/// _main.&lt;n&gt;.ok is written last: the game only trusts a save that has it; each file is flushed to disk, and the
/// folder's names before and after the marker (DirectorySync, Linux and macOS)
/// </description></item>
/// <item><description>
/// the save is read back and compared with the edits; on any mismatch (or failure) the new files are removed and the
/// world is as it was: nothing of the old save is touched before this
/// </description></item>
/// <item><description>
/// the save it was made from and the chunk files it replaced are removed (KeepBase: kept, for the open world's next
/// saves, which are made from it too), and the files of Drop (an earlier save of the same session) with them
/// </description></item>
/// </list>
/// No backup folder: nothing is removed before the new save has read back right.
/// </summary>
public static partial class WorldWriter
{
    /// <summary>Prefab hash of a player's tombstone, which zone resets never clear.</summary>
    public static readonly int TombstonePrefab = StableHash.Of("Player_tombstone");

    /// <summary>The outcome of a <c>Save</c>.</summary>
    /// <param name="Saved">Whether a new save was written.</param>
    /// <param name="Message">What was saved, or why nothing was, for the user, in the language in use.</param>
    /// <param name="BackupDirectory">Always null: the writer makes no backup folder.</param>
    /// <param name="ZonesWritten">Zones whose existing terrain objects got new ground data.</param>
    /// <param name="ZonesCreated">Zones that got a new terrain object.</param>
    /// <param name="Skipped">Edits that could not be saved, each with the reason.</param>
    /// <param name="ObjectsDeleted">Objects left out, those cleared by zone resets included.</param>
    /// <param name="ObjectsAdded">New objects written.</param>
    /// <param name="ZonesReset">Zones handed back to the world generator.</param>
    public sealed record Result(bool Saved, string Message, string? BackupDirectory, int ZonesWritten, int ZonesCreated, List<string> Skipped, int ObjectsDeleted = 0, int ObjectsAdded = 0, int ZonesReset = 0)
    {
        /// <summary>The files the save wrote (its _main files and new chunk files).</summary>
        public IReadOnlyList<string> Files { get; init; } = [];
        /// <summary>The save number written (-1: none).</summary>
        public int Number { get; init; } = -1;
        /// <summary>The new save as it was read back to check it (null when nothing was saved): no need to read it again.</summary>
        public WorldSave? Read { get; init; }

        /// <summary>The message in both languages (a result made with only a message: that text on both sides).</summary>
        public Said Report { get => field ?? Said.Raw(Message); init; }
    }

    /// <summary>A save refused or failed: nothing written, why in both languages.</summary>
    /// <param name="why">Why nothing was saved.</param>
    /// <param name="skipped">The edits that could not be saved so far.</param>
    private static Result Refused(Said why, List<string> skipped)
    {
        return new Result(false, why.Shown, null, 0, 0, skipped) { Report = why };
    }

    /// <summary>How a save treats the files around it.</summary>
    /// <param name="KeepBase">The save it is made from stays (an open world saves again from it).</param>
    /// <param name="Drop">
    /// Files of an earlier save of the same session, removed once this one has read back right.
    /// </param>
    /// <param name="Latest">
    /// The newest save number the caller knows of (its own last save, or the one it opened); a newer one in the folder
    /// was made by the game since, and saving would throw it away, so nothing is saved.
    /// </param>
    public sealed record Options(bool KeepBase = false, IReadOnlyCollection<string>? Drop = null, int? Latest = null);

    /// <summary>Writes a new save of the world with these changes, or nothing at all (see the type's steps).</summary>
    /// <param name="world">The world as loaded; its save is the base of the new one.</param>
    /// <param name="changed">Zones whose ground was edited.</param>
    /// <param name="deleted">Ids (WorldSave.ObjectRefs) of objects to leave out of the new save.</param>
    /// <param name="added">
    /// New objects (WorldSave.NewObjectBytes): their own data when they carry it (Raw), else a copy of an
    /// object of the same prefab in the world, else a blank object from the game's data.
    /// </param>
    /// <param name="resets">
    /// Zones handed back to the world generator (their objects are removed and the zone is marked as not generated).
    /// </param>
    /// <param name="options">File handling; null for the defaults.</param>
    public static Result Save(WorldSave world, IReadOnlyList<ZoneEdit> changed, IReadOnlyCollection<int>? deleted = null, IReadOnlyList<NewObject>? added = null, IReadOnlyList<ZoneReset>? resets = null, Options? options = null)
    {
        options ??= new Options();
        List<string> skipped = [];
        deleted ??= [];
        added ??= [];
        resets ??= [];
        // Terrain edits of zones whose ground is reset are dropped.
        HashSet<(int, int)> groundReset = [.. resets.Where(r => r.Ground).Select(r => (r.X, r.Z))];
        changed = [.. changed.Where(e => !groundReset.Contains((e.ZoneX, e.ZoneZ)))];
        if (Refusal(world, changed, deleted, added, resets, options, skipped) is Result refused)
        {
            return refused;
        }
        var plan = new Planned(changed, groundReset, skipped);
        PlanRemovals(world, deleted, resets, plan);
        Dictionary<(int, int), List<TerrainZone>> existing = world.TerrainZones
            .Where(z => z.Source != null && !(plan.Removals.TryGetValue(z.Source.File, out var gone) && gone.Contains(z.Source.Start)))
            .GroupBy(z => (z.ZoneX, z.ZoneZ)).ToDictionary(g => g.Key, g => g.ToList());

        // Plan: which chunk files change, and how. Reading the template and the models can fail (a chunk file gone,
        // data that does not parse): nothing is written yet, and the caller gets a result, not an exception.
        Func<int, int, ChunkFile?> findChunk = ChunkMath.Finder(world.Chunks, world.GridWidth);
        try
        {
            PlanTerrainEdits(world, plan, existing, findChunk);
            PlanNewObjects(world, plan, added, findChunk);
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "making the save's changes (nothing saved)");
            return Refused(Said.Of("save.writer.failed", ex.Message), skipped);
        }
        if (plan.Written + plan.Created + plan.Removed + plan.AddedCount + resets.Count == 0)
        {
            return Refused(Said.Of("save.writer.nothingSaved"), skipped);
        }
        if (Commit(world, plan, resets) is Result failed)
        {
            return failed;
        }
        RemoveOld(world, plan, options);
        var saved = Said.Of("save.writer.saved", Summary(plan, resets), plan.NewNumber);
        return new Result(true, saved.Shown, null, plan.Written, plan.Created, skipped, plan.Removed, plan.AddedCount, resets.Count) { Files = plan.NewFiles, Number = plan.NewNumber, Read = plan.Read, Report = saved };
    }

    /// <summary>The reason a save is refused before anything is planned, or null when it may go on.</summary>
    internal static Result? Refusal(WorldSave world, IReadOnlyList<ZoneEdit> changed, IReadOnlyCollection<int> deleted, IReadOnlyList<NewObject> added, IReadOnlyList<ZoneReset> resets, Options options, List<string> skipped)
    {
        if (resets.Count > 0 && world.Zones == null)
        {
            return Refused(Said.Of("save.writer.noDb"), skipped);
        }
        if (changed.Count == 0 && deleted.Count == 0 && added.Count == 0 && resets.Count == 0)
        {
            return Refused(Said.Of("save.writer.nothing"), skipped);
        }
        if (options.Latest is int known && Newer(world.Directory, known) is Said newer)
        {
            return Refused(newer, skipped);
        }
        // Each file keeps the format it was read in, as the game leaves the chunk files it does not write again: the
        // formats the writer knows lay objects out the same, so objects copied or made here fit either.
        if (UnwritableFormat(world) is int format)
        {
            return Refused(Said.Of("save.writer.format", format, WorldVersion.OldestWritable, WorldVersion.Latest), skipped);
        }
        return null;
    }
}
