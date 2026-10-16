using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using TerrainService = ValheimGen.TerrainService;

namespace TerrainEditor.Desktop;

/// <summary>
/// One open world, for as long as it is open: the save (or the running game's world, live), the edits
/// waiting to be saved, and the world generator's ground. Every area opened from the map shares it, so
/// edits stay pending while you move around the world (like the web editor's session).
/// </summary>
public sealed class WorldSession : IDisposable
{
    /// <summary>The world as read; replaced when it is read again (a save that rereads, zone resets, Reload, Discard).</summary>
    public required WorldSave World { get; set; }
    /// <summary>The edits waiting to be saved (live: to be applied), shared by every area.</summary>
    public required EditStore Edits { get; init; }
    /// <summary>The ground the world's locations and pieces flatten; replaced when they change.</summary>
    public required TerrainModifiers Modifiers { get; set; }
    /// <summary>The world generator's ground, with <c>Modifiers</c> applied.</summary>
    public required TerrainService Terrain { get; init; }
    /// <summary>Live: the running game (through WorldEditorBridge); null offline.</summary>
    public LiveBridge? Live { get; init; }
    /// <summary>Live: which object changes the game already has.</summary>
    public LiveSync LiveSync { get; } = new();
    /// <summary>The world is the running game's (Apply live, not Save).</summary>
    public bool IsLive => Live != null;
    /// <summary>
    /// Where the world comes from: the world's name offline, the connection's name live ("my game"...), shown in
    /// the map's title and part of a live world's history key.
    /// </summary>
    public string Label { get; init; } = "";

    /// <summary>
    /// The history while no area is open, or another one (see EditSession.Export); gone when the world
    /// is read again (zone resets, No limit ground). It is kept on disk after each save or Apply live
    /// (HistoryFile) and comes back when the world is opened again.
    /// </summary>
    public EditSession.Kept? History { get; set; }
    /// <summary>The open area's session, whose history is the world's while it is open.</summary>
    public EditSession? Area { get; set; }
    /// <summary>The history kept from an earlier session of the editor, when the world was opened (null: none).</summary>
    public HistoryFile.Restored? Restored { get; private set; }
    /// <summary>
    /// Its steps may not match the world any more (live, or saved by the game since): asked once before
    /// undoing or redoing them (Earlier).
    /// </summary>
    public bool EarlierMayDiffer => Restored?.Changed == true && !EarlierAllowed;
    /// <summary>The user said yes once to undoing or redoing the earlier session's steps; not asked again.</summary>
    public bool EarlierAllowed { get; set; }
    /// <summary>The window said so already (that a history from an earlier session came back).</summary>
    public bool RestoredSaid { get; set; }

    /// <summary>Ids for objects added in this session: negative, like the web editor's, unique across every area.</summary>
    private int _nextId = -1;
    /// <summary>The next id for an added object (-1, -2...); thread-safe.</summary>
    public int NextId() => Interlocked.Decrement(ref _nextId) + 1;

    /// <summary>A session for a world just read, with its kept history restored.</summary>
    private static WorldSession From(WorldSave world, LiveBridge? live, string label)
    {
        var modifiers = new TerrainModifiers(world);
        var s = new WorldSession
        {
            World = world,
            Edits = new EditStore(world),
            Modifiers = modifiers,
            Terrain = new TerrainService(world, modifiers),
            Live = live,
            Label = label,
        };
        s.RestoreHistory();
        return s;
    }

    /// <summary>The history kept on disk for this world, if any (the first area opened takes it).</summary>
    private void RestoreHistory()
    {
        Restored = HistoryFile.Read(this);
        History = Restored?.Kept;
    }

    /// <summary>
    /// After a save or Apply live: the history as it is now (it matches what the world holds) goes to
    /// disk. A failure to write it is only logged: the save itself is done.
    /// </summary>
    /// <param name="later">Apply live: taken now, written in the background (<see cref="HistoryFile.WriteLater"/>),
    /// since it can come at every edit.</param>
    private void KeepHistory(bool later = false)
    {
        try
        {
            if ((Area?.Export() ?? History) is { } kept)
            {
                if (later)
                {
                    HistoryFile.WriteLater(this, kept);
                }
                else
                {
                    HistoryFile.Write(this, kept);
                }
            }
        }
        catch (Exception ex)
        {
            // The save itself is done: any failure here is logged (an unexpected one in full), not reported as a
            // failed save.
            Console.WriteLine($"History: could not keep it on disk: {(ex is IOException or UnauthorizedAccessException ? ex.Message : ex.ToString())}");
        }
    }

    /// <summary>
    /// Saved or applied from the map (no area open): the kept steps are in the world now, as an open
    /// area's are once it saves (EditSession), so a later Discard does not undo them.
    /// </summary>
    /// <param name="sent">After a partial Apply live, whether the game has all a step did (null: every step).</param>
    private void MarkHistoryApplied(Func<EditSession.Change, bool>? sent = null)
    {
        if (Area == null && History != null)
        {
            // Applied is part of a step's hash: the ids and frames are keyed again once it is set.
            var ids = History.Ids.ToList();
            var frames = History.Frames?.ToList();
            EditSession.MarkSent(History.Undo, sent ?? (static _ => true));
            History = History with { Ids = ids.ToDictionary(p => p.Key, p => p.Value), Frames = frames?.ToDictionary(p => p.Key, p => p.Value) };
        }
    }

    /// <summary>The world was read again: the steps do not match it any more (the kept file is deleted too).</summary>
    private void ForgetHistory()
    {
        History = null;
        Restored = null;
        HistoryFile.Delete(this);
    }

    /// <summary>
    /// The game's data, needed before any world opens: refused without Valheim (the ground near
    /// locations, and the edits saved there, depend on it). Throws <c>InvalidOperationException</c> with
    /// what is missing.
    /// </summary>
    private static void NeedGame()
    {
        if (!GameData.Ensure(GameLook.Bundles))
        {
            throw new InvalidOperationException(GameData.Missing);
        }
        WorldSave.ModifierPrefabs = [.. TerrainModifiers.NetworkPrefabHashes];
        ReadModFiles();
    }

    /// <summary>
    /// What the editor reads from the player's mods as a world or the Workshop opens: the data names their code holds
    /// (ModKeys, read again only when a mod changes), their worldeditor.json manifests (ModManifests) and the editor
    /// extensions they offer that the user allowed (ModExtensions).
    /// </summary>
    public static void ReadModFiles()
    {
        var settings = AppSettings.Load();
        ModKeys.Refresh(settings);
        ModManifests.Refresh(settings);
        ModExtensions.Refresh(settings);
    }

    /// <summary>A world on this computer (its folder). Throws when the game's data is missing.</summary>
    public static WorldSession Open(string dir)
    {
        NeedGame();
        var world = WorldSave.Load(dir);
        WorldSave.Builder = Builders.Default(world);
        return From(world, null, world.Name);
    }

    /// <summary>The world of a running game or server (live).</summary>
    /// <param name="live">The connection to the game's WorldEditorBridge; the session owns it from here.</param>
    /// <param name="label">The connection's name, for <c>Label</c>.</param>
    public static async Task<WorldSession> OpenLive(LiveBridge live, string label)
    {
        await Task.Run(NeedGame);
        await LearnMods(live);
        var mods = await BridgeMods(live);
        var world = await live.LoadWorld();
        WorldSave.Builder = Builders.Default(world);
        var session = From(world, live, label);
        // Once the session is there to end it (Dispose): a world that failed to load leaves no game connected.
        ModExtensions.LiveConnected(new LiveGame(live, mods));
        return session;
    }

    /// <summary>
    /// The mods' plugins registered with the bridge (GET /mods; none from an older plugin, or when it cannot be asked:
    /// the extensions still get the game), for ModExtensions.LiveConnected once the session is open.
    /// </summary>
    private static async Task<List<string>> BridgeMods(LiveBridge live)
    {
        List<string> mods = [];
        try
        {
            (_, mods) = await live.Mods();
            if (mods.Count > 0)
            {
                Console.WriteLine($"bridge mods: {string.Join(", ", mods)}.");
            }
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            Console.WriteLine($"bridge mods: not read from the game ({e.Message}).");
        }
        return mods;
    }

    /// <summary>
    /// Learns the prefabs the game's mods add (GET /catalogue) and what the game draws of them (POST /models), before
    /// the world is read so its objects get their names and models. A plugin too old to tell, or a report that cannot
    /// be read, leaves what is known as it is.
    /// </summary>
    private static async Task LearnMods(LiveBridge live)
    {
        try
        {
            string report = await live.Catalogue();
            int mods = await Task.Run(() => GameData.AddMods(report));
            Console.WriteLine($"mod catalogue: the game has {mods} mod prefabs ({ModCatalogue.Count} known in all).");
            if (mods > 0)
            {
                string models = await live.Models(ModCatalogue.Names);
                Console.WriteLine($"mod catalogue: {ModCatalogue.TakeModels(models)} models from the game.");
            }
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"mod catalogue: not read from the game ({e.Message}).");
        }
    }

    /// <summary>
    /// What is waiting to be saved (live: to be applied to the game): changed zones, deletions, added objects,
    /// zone resets. Once saved offline, the objects count what differs from that save.
    /// </summary>
    public (int Zones, int Deleted, int Added, int Resets) Pending
    {
        get
        {
            if (IsLive)
            {
                var (d, a) = LiveSync.Pending(Edits);
                return (Edits.ChangedZoneCount, d, a, Edits.ResetCount);
            }
            if (_lastSave == null)
            {
                return (Edits.ChangedZoneCount, Edits.DeletedCount, Edits.AddedCount, Edits.ResetCount);
            }
            // Saved already: what differs from that save (an undo after it is pending too).
            // Counted against the saved sets only (no set of the current ones): after a save with millions of
            // new objects this runs at every change. Both lists hold each object once.
            var deleted = Edits.Deleted;
            var added = Edits.Added;
            int keptDel = deleted.Count(_savedDeleted.Contains), keptAdd = added.Count(_savedAdded.Contains);
            int del = deleted.Count - keptDel + _savedDeleted.Count - keptDel;
            int add = added.Count - keptAdd + _savedAdded.Count - keptAdd;
            return (Edits.ChangedZoneCount, del, add, Edits.ResetCount);
        }
    }

    /// <summary>Offline: whether a new object is in the last save as it is now (not changed since).</summary>
    /// <param name="id">The new object's id (negative).</param>
    public bool IsSaved(int id) => _lastSave != null && Edits.FindAdded(id) is { } o && _savedAdded.Contains(o);

    // Offline, once saved: the open world saves again from the save it was opened from (kept until the
    // world is left), with every change since; the zones and objects of the last save, the files it
    // wrote (removed by the next one).
    /// <summary>The files the session's last save wrote (removed by the next one); null before any save.</summary>
    private IReadOnlyList<string>? _lastSave;
    /// <summary>The last save's number, the one a newer game save is checked against.</summary>
    private int? _lastNumber;
    /// <summary>The zones the last save wrote changes for (written again by the next).</summary>
    private HashSet<(int, int)> _savedZones = [];
    /// <summary>The deletions as of the last save.</summary>
    private HashSet<int> _savedDeleted = [];
    /// <summary>The added objects as of the last save.</summary>
    private HashSet<NewObject> _savedAdded = [];

    /// <summary>Forgets the session's saves (the world was read again, or left).</summary>
    private void ForgetSaves()
    {
        _lastSave = null;
        _lastNumber = null;
        _savedZones = [];
        _savedDeleted = [];
        _savedAdded = [];
    }

    /// <summary>What a save or Apply live did.</summary>
    /// <param name="Done">Saved, or something was applied.</param>
    /// <param name="Message">What to tell the user.</param>
    /// <param name="Reloaded">
    /// The world was read again (a save after zone resets or No limit ground; live, after zone resets only):
    /// areas must be read again too. Live No limit ground shows in <see cref="Lifted"/> alone, which callers
    /// check as well.
    /// </param>
    /// <param name="Saved">The writer's result (offline only).</param>
    public sealed record Outcome(bool Done, string Message, bool Reloaded, WorldWriter.Result? Saved = null)
    {
        /// <summary>No limit ground was turned into ground discs: the ground under the area changed.</summary>
        public bool Lifted { get; init; }
        /// <summary>Live: not applied, the answer to "apply over the game's changes?" was no.</summary>
        public bool Declined { get; init; }
        /// <summary>
        /// Live: the parts that did not go in after others did (Done, with the error in the message); the history
        /// marks as applied only the steps the game has all of.
        /// </summary>
        public LiveParts Unsent { get; init; }

        /// <summary>Live, with <see cref="LiveParts.Resets"/> in <see cref="Unsent"/>: the zones whose reset did not go in.</summary>
        public IReadOnlySet<(int X, int Z)> UnsentResets { get; init; } = new HashSet<(int X, int Z)>();

        /// <summary>
        /// Whether the game has all a step did: nothing of it is in the parts that did not go in (a step's reset of a
        /// zone not waiting any more, unmarked since, is not). With the objects not sent after No limit ground became
        /// ground discs, no step has all it did: the discs are not in the game, and no step says which made them.
        /// </summary>
        public bool Sent(EditSession.Change c) =>
            !(Lifted && Unsent.HasFlag(LiveParts.Objects))
            && (!Unsent.HasFlag(LiveParts.Objects) || c.Things.Length == 0)
            && !c.Resets.Any(r => UnsentResets.Contains((r.Reset.X, r.Reset.Z)));
    }

    /// <summary>The parts of an Apply live sent after the ground.</summary>
    [Flags]
    public enum LiveParts
    {
        /// <summary>None of them.</summary>
        None = 0,
        /// <summary>The objects made, removed or changed.</summary>
        Objects = 1,
        /// <summary>The zone resets.</summary>
        Resets = 2,
    }

    /// <summary>No limit ground turned into ground discs (<see cref="LiftToDiscs"/>), with what to put back if nothing goes out.</summary>
    /// <param name="Plan">The discs and edits made.</param>
    /// <param name="Before">The edits as they were.</param>
    /// <param name="Modifiers">The flattening modifiers as they were.</param>
    /// <param name="Version">The edits' version just after: changed since, they are not put back.</param>
    private sealed record Lifting(Uplift.Plan Plan, EditStore.Checkpoint Before, TerrainModifiers Modifiers, int Version);

    /// <summary>
    /// No limit ground waiting (lifts): turned into ground discs and ordinary edits (Uplift), the ground
    /// worked out again with them. Null when there was none.
    /// </summary>
    private Lifting? LiftToDiscs()
    {
        var plan = Uplift.Make(World, Terrain, Edits, NextId);
        if (plan == null)
        {
            return null;
        }
        var before = Edits.Mark();
        var modifiers = Modifiers;
        Uplift.Apply(plan, Edits);
        UseModifiers(new TerrainModifiers(Uplift.PlacedNow(World, Edits)));
        return new Lifting(plan, before, modifiers, Edits.Version);
    }

    /// <summary>
    /// A save or apply that wrote or sent nothing: the No limit ground it turned into discs is put back as it was,
    /// unless the edits changed since (a stroke drawn while the game answered).
    /// </summary>
    /// <returns>Put back.</returns>
    private bool Unlift(Lifting lift)
    {
        if (Edits.Version != lift.Version)
        {
            return false;
        }
        Edits.Restore(lift.Before);
        UseModifiers(lift.Modifiers);
        return true;
    }

    /// <summary>Switches the session and its generator to new flattening modifiers.</summary>
    private void UseModifiers(TerrainModifiers modifiers)
    {
        Modifiers = modifiers;
        Terrain.UseModifiers(modifiers);
    }

    /// <summary>
    /// Writes everything into the world's files, like Apply live: the world stays open as it is, with
    /// its history (an undo, saved again, is saved). Each save is made from the save the world was opened
    /// from, with every change since (so a zone saved before and not changed since is written again),
    /// and the previous save of the session goes. After zone resets or when No limit ground became ground
    /// discs, the world is read again from what was written (the ground or zones are not the same).
    /// A save the game made after the world was read (a play test between two saves) is never thrown
    /// away: the save is refused before anything changes, No limit ground included.
    /// </summary>
    public Outcome Save()
    {
        int known = _lastNumber ?? World.SaveNumber;
        if (WorldWriter.Newer(World.Directory, known) is string newer)
        {
            return new Outcome(false, newer, false, new WorldWriter.Result(false, newer, null, 0, 0, []));
        }
        var lift = LiftToDiscs();
        // The changed zones and those the last save wrote (each copied once).
        var changed = Edits.Copies(e => e.Changed || _savedZones.Contains((e.ZoneX, e.ZoneZ)));
        var zones = changed.Where(e => e.Changed).Select(e => (e.ZoneX, e.ZoneZ)).ToHashSet();
        zones.UnionWith(_savedZones);
        bool reread = lift != null || Edits.ResetCount > 0;
        WorldWriter.Result result;
        try
        {
            result = WorldWriter.Save(World, changed, Edits.Deleted, Edits.Added, Edits.Resets, new WorldWriter.Options(KeepBase: !reread, Drop: _lastSave, Latest: known));
        }
        catch
        {
            if (lift != null)
            {
                Unlift(lift);
            }
            throw;
        }
        // Not saved: the No limit ground stays as it was (the area and its history too), unless the edits changed
        // meanwhile: then it stays as discs, and the area is read again.
        if (!result.Saved && lift != null && Unlift(lift))
        {
            lift = null;
        }
        if (result.Saved && reread)
        {
            World = WorldSave.Load(World.Directory);
            WorldWriter.Prune(World.Directory);
            Edits.ResetFrom(World);
            UseModifiers(new TerrainModifiers(World));
            _nextId = -1;
            ForgetHistory();
            ForgetSaves();
        }
        else if (result.Saved)
        {
            _lastSave = result.Files;
            _lastNumber = result.Number;
            _savedZones = zones;
            Edits.MarkApplied(changed.Select(e => (e.ZoneX, e.ZoneZ)));
            _savedDeleted = [.. Edits.Deleted];
            _savedAdded = [.. Edits.Added];
            MarkHistoryApplied();
            KeepHistory();
        }
        string message = lift != null ? $"{result.Message} No limit ground: {lift.Plan.Describe()}." : result.Message;
        return new Outcome(result.Saved, message, result.Saved && reread, result with { Message = message }) { Lifted = lift != null };
    }

    /// <summary>Leaving an offline world it saved: the save it was opened from goes (only the latest stays).</summary>
    private void PruneSaves()
    {
        if (!IsLive && _lastSave != null && Directory.Exists(World.Directory))
        {
            // The world is left whatever happens: a folder that cannot be read now keeps its files.
            try
            {
                WorldWriter.Prune(World.Directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                Console.WriteLine($"Leaving the world: its earlier saves could not be removed: {ex.Message}");
            }
        }
        ForgetSaves();
    }

    /// <summary>
    /// Apply live's lock. One apply at a time: a second one (a double click) waits and sends only what is
    /// still pending. Held while applying (<c>MergeGame</c> waits for it).
    /// </summary>
    private readonly SemaphoreSlim _applying = new(1, 1);

    /// <summary>
    /// Counts each Apply live's start and end: a read of the game taken before or during an apply (<c>FetchGame</c>
    /// notes the count) has not got what it sent, and <c>MergeGame</c> drops it.
    /// </summary>
    private int _applies;

    /// <summary>
    /// When the world is left: the save it was opened from goes (offline, once saved again) and the live connection closes.
    /// </summary>
    public void Dispose()
    {
        if (Live != null)
        {
            ModExtensions.LiveDisconnected();
        }
        PruneSaves();
        Live?.Dispose();
        _applying.Dispose();
    }

    /// <summary>
    /// Live: the changed zones' ground and the object changes go into the running game; zone resets last
    /// (the game regenerates them), after which the world is read again from the game.
    /// </summary>
    /// <param name="confirm">Asked, with the zones, when applying would write over ground the game changed under
    /// the editor's changes (asked here, while no follow can come in between); null applies without asking.</param>
    public async Task<Outcome> ApplyLive(Func<IReadOnlyList<(int X, int Z)>, Task<bool>>? confirm = null)
    {
        await _applying.WaitAsync();
        Interlocked.Increment(ref _applies);
        try
        {
            if (confirm != null && Edits.PendingOverGame() is { Count: > 0 } over && !await confirm(over))
            {
                return new Outcome(false, "Not applied: the game changed the ground where you have changes (Apply live asks again).", false) { Declined = true };
            }
            return await ApplyLiveOnce();
        }
        finally
        {
            Interlocked.Increment(ref _applies);
            _applying.Release();
        }
    }

    /// <summary>One Apply live, with the lock held; a failure of the game's answer comes back as the outcome.</summary>
    private async Task<Outcome> ApplyLiveOnce()
    {
        var live = Live!;
        var done = new List<string>();
        bool reloaded = false;
        var lift = LiftToDiscs();
        var plan = lift?.Plan;
        if (plan != null)
        {
            done.Add($"No limit ground: {plan.Describe()}");
        }
        // Until the ground or objects went in (sent), or the ground may have (unsure), a failure has sent nothing: the
        // No limit ground is put back as it was.
        bool sent = false, unsure = false, objectsDone = false;
        try
        {
            // Copies as sent: edits made while the game answers are not marked as applied.
            var zones = Edits.Copies(e => e.Changed);
            if (zones.Count > 0)
            {
                try
                {
                    await live.ApplyTerrain(zones.Select(e => (e.ZoneX, e.ZoneZ, WorldWriter.EncodeTerrain(e))).ToList());
                }
                catch (Exception ex) when (!NothingSent(ex))
                {
                    // No answer, or one after the game may have taken the ground: the discs stay (the area is read
                    // again), and the next Apply live sends the ground again, which only sets each zone's ground anew.
                    unsure = true;
                    throw;
                }
                sent = true;
                Edits.MarkApplied(zones);
                done.Add($"{zones.Count} zone(s) of ground");
            }
            string objects = await LiveSync.Apply(World, Edits, live);
            objectsDone = true;
            if (objects != "")
            {
                sent = true;
                done.Add(objects);
            }
            if (Edits.ResetCount > 0)
            {
                var resets = Edits.Resets;
                await live.ResetZones(resets);
                // Done in the game: not pending any more, even if reading the world below fails (sent again, they
                // would wipe what players built there since).
                Edits.MarkResetsApplied(resets);
                done.Add($"{resets.Count} zone(s) reset");
                try
                {
                    World = await live.LoadWorld();
                }
                catch (Exception ex) when (GameFailed(ex))
                {
                    MarkHistoryApplied();
                    KeepHistory(later: true);
                    return new Outcome(true, $"Applied to the running game: {string.Join("; ", done)}; but the world could not be read again from the game ({ex.Message}): open it again to see the reset zones.", false) { Lifted = plan != null };
                }
                Edits.ResetFrom(World);
                UseModifiers(new TerrainModifiers(World));
                LiveSync.Reset();
                _digests.Clear();
                _nextId = -1;
                ForgetHistory();
                reloaded = true;
            }
            else if (done.Count > 0)
            {
                MarkHistoryApplied();
                KeepHistory(later: true);
            }
            return new Outcome(done.Count > 0, done.Count > 0 ? $"Applied to the running game: {string.Join("; ", done)}." : "Nothing to apply.", reloaded) { Lifted = plan != null };
        }
        // The game answering with an error (InvalidOperationException from the bridge) is reported too.
        catch (Exception ex) when (GameFailed(ex))
        {
            if (sent)
            {
                // Some parts went in: said, and the steps the game has all of are applied.
                var resets = Edits.Resets.Select(r => (r.X, r.Z)).ToHashSet();
                var partly = new Outcome(true, $"Applied to the running game: {string.Join("; ", done)}; but then it failed ({ex.Message}): Apply live again for the rest.", false)
                {
                    Lifted = lift != null,
                    // Resets only while some are still to send: a reset unmarked since leaves none, though steps keep it.
                    Unsent = (objectsDone ? LiveParts.None : LiveParts.Objects) | (resets.Count > 0 ? LiveParts.Resets : LiveParts.None),
                    UnsentResets = resets,
                };
                MarkHistoryApplied(partly.Sent);
                KeepHistory(later: true);
                return partly;
            }
            bool lifted = lift != null && (unsure || !Unlift(lift));
            return new Outcome(false, "Could not apply live: " + ex.Message, false) { Lifted = lifted };
        }
    }

    /// <summary>
    /// A failure of the ground call that shows the game took none of it: no connection could be made, or the plugin
    /// turned it down before reading it (4xx or 503). A 500, or an answer broken off or too late, may come after the
    /// game took the ground.
    /// </summary>
    private static bool NothingSent(Exception ex) =>
        ex is HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError }
            or BridgeAnswerException { Status: (>= 400 and < 500) or 503 };

    /// <summary>The game unreachable or answering with an error (InvalidOperationException from the bridge).</summary>
    private static bool GameFailed(Exception ex) =>
        ex is HttpRequestException or IOException or TaskCanceledException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException;

    // ---- Live: following the game. The zones shown are asked about every few seconds (one number each,
    // cheap); those that changed are read again (FetchGame, on the network) and merged in (MergeGame, at
    // once on the window's thread): objects made, removed or changed in the game appear as they are, and
    // ground changed there replaces the editor's where it has no changes of its own (where it has, they
    // are kept, and applying them asks first: Edits.PendingOverGame).
    /// <summary>Each zone's digest as last merged (the game's one number per zone); cleared when the world is read again.</summary>
    private readonly Dictionary<(int, int), ulong> _digests = [];

    /// <summary>What <c>FetchGame</c> read from the game, for <c>MergeGame</c>.</summary>
    /// <param name="World">The world it was read for (dropped if the world was read again since).</param>
    /// <param name="Digests">Every zone asked about, with its digest now.</param>
    /// <param name="Changed">The zones whose digest changed.</param>
    /// <param name="Objects">The changed zones' objects, as the game sends them (owner, id, data).</param>
    /// <param name="Applies">Apply live's count when the read began (dropped if an apply began or ended since).</param>
    public sealed record Fetched(WorldSave World, Dictionary<(int X, int Z), ulong> Digests, List<(int X, int Z)> Changed, List<(long User, uint Id, byte[] Bytes)> Objects, int Applies);

    /// <summary>What a merge of the game's changes did.</summary>
    /// <param name="Merged">The objects added, updated and vanished in the world.</param>
    /// <param name="Ours">The editor's own new objects the game removed (players picked or destroyed them).</param>
    /// <param name="Ground">Zones whose ground came in.</param>
    /// <param name="Kept">Zones whose ground the game changed under the editor's changes.</param>
    public sealed record Followed(WorldSave.Merged Merged, List<int> Ours, List<(int X, int Z)> Ground, List<(int X, int Z)> Kept)
    {
        /// <summary>Anything changed at all.</summary>
        public bool Any => Merged.Added.Count + Merged.Updated.Count + Merged.Vanished.Count + Ours.Count + Ground.Count + Kept.Count > 0;
    }

    /// <summary>
    /// The zones that changed since they were last merged, read from the game (null: none, or not live). The
    /// plugin's errors and an unreachable game are the caller's to show.
    /// </summary>
    public async Task<Fetched?> FetchGame(IReadOnlyCollection<(int X, int Z)> zones)
    {
        if (Live is not { } live || zones.Count == 0)
        {
            return null;
        }
        var world = World;
        int applies = Volatile.Read(ref _applies);
        var now = await live.Watch(zones);
        var changed = now.Where(kv => !_digests.TryGetValue(kv.Key, out ulong d) || d != kv.Value).Select(kv => kv.Key).ToList();
        if (changed.Count == 0)
        {
            return null;
        }
        return new Fetched(world, now, changed, await live.ZoneObjects(changed), applies);
    }

    /// <summary>
    /// What was read, merged into the world (on the window's thread, at once: nothing else changes the
    /// world meanwhile). Null: not now (an apply is under way, or the world's lists are being read): merge
    /// it again later. Read for a world read again since, or before or during an Apply live: dropped (empty).
    /// A read older than an apply lacks what it sent (the new objects would count as removed by players);
    /// its zones' digests are not kept, so they are read again.
    /// </summary>
    public Followed? MergeGame(Fetched f)
    {
        if (f.World != World)
        {
            return new Followed(new WorldSave.Merged([], [], [], []), [], [], []);
        }
        // Held while merging: an apply (perhaps on another thread: Claude, a script) can neither begin nor end
        // between the check of the count and the merge.
        if (!_applying.Wait(0))
        {
            return null;
        }
        try
        {
            return f.Applies != Volatile.Read(ref _applies) ? new Followed(new WorldSave.Merged([], [], [], []), [], [], []) : Merge(f);
        }
        finally
        {
            _applying.Release();
        }
    }

    /// <summary><see cref="MergeGame"/> for a read taken with no Apply live begun or ended since, under the apply lock.</summary>
    private Followed? Merge(Fetched f)
    {
        var deleted = Edits.Deleted;
        // Kept as the editor has them: its applied deletions (undo brings them back).
        var merged = World.MergeLive(f.Changed, f.Objects, LiveSync.IsDestroyed, LiveSync.IsOurs);
        if (merged == null)
        {
            return null;
        }
        foreach (var key in f.Changed)
        {
            _digests[key] = f.Digests[key];
        }
        // Gone from the game while deleted in the editor, not applied yet: nothing left to delete.
        Edits.SetDeleted([.. merged.Vanished.Where(deleted.Contains)], false);
        // The editor's new objects the game no longer has in the zones read.
        var changed = f.Changed.ToHashSet();
        var there = f.Objects.Select(o => (o.User, o.Id)).ToHashSet();
        var ours = LiveSync.NewObjects().Where(n => changed.Contains(Zone(n.Target.Position)) && !there.Contains((n.Target.User, n.Target.Id))).Select(n => n.Id).ToList();
        foreach (int id in ours)
        {
            LiveSync.Forget(id);
        }
        Edits.SetDeleted(ours, true);
        var ground = new List<(int, int)>();
        var kept = new List<(int, int)>();
        foreach (var (x, z) in merged.TerrainZones)
        {
            var zone = World.TerrainZones.FirstOrDefault(t => t.ZoneX == x && t.ZoneZ == z);
            switch (Edits.TakeGameGround(x, z, zone))
            {
                case EditStore.GameGround.Taken: ground.Add((x, z)); break;
                case EditStore.GameGround.Kept: kept.Add((x, z)); break;
            }
        }
        ground.AddRange(Edits.TakeWaitingGround());
        if (merged.Modifiers)
        {
            // (With the editor's ground discs: those it applied are its own, not merged.)
            UseModifiers(new TerrainModifiers(Uplift.PlacedNow(World, Edits)));
        }
        return new Followed(merged, ours, ground, kept);
    }

    /// <summary>Zones whose changes of the editor's were undone since the game changed their ground: theirs now.</summary>
    public List<(int X, int Z)> TakeWaitingGround() => Edits.TakeWaitingGround();

    /// <summary>The zone a world position is in (zones are 64 m, centred on multiples of 64).</summary>
    private static (int, int) Zone(System.Numerics.Vector3 p) => ((int)MathF.Floor((p.X + 32f) / 64f), (int)MathF.Floor((p.Z + 32f) / 64f));

    /// <summary>Live: the world read again from the game (what players changed since); pending edits are dropped.</summary>
    public async Task Reload()
    {
        World = await Live!.LoadWorld();
        Edits.ResetFrom(World);
        UseModifiers(new TerrainModifiers(World));
        LiveSync.Reset();
        _digests.Clear();
        History = null;
    }

    /// <summary>
    /// Drops every pending change (offline: the world is read again from its files, and the history kept at the
    /// last save comes back).
    /// </summary>
    public void Discard()
    {
        if (!IsLive)
        {
            PruneSaves();
            World = WorldSave.Load(World.Directory);
        }
        Edits.ResetFrom(World);
        UseModifiers(new TerrainModifiers(World));
        LiveSync.Reset();
        _digests.Clear();
        History = null;
        Area = null;
        // Offline the world is as it was last saved: so is the history kept then.
        if (!IsLive)
        {
            _nextId = -1;
            RestoreHistory();
        }
    }
}
