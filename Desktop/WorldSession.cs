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
public sealed partial class WorldSession : IDisposable
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
        world.Rules = ExpandWorld.Resolve(world, AppSettings.Load());
        WorldSave.Builder = Builders.Default(world);
        return From(world, null, world.Name);
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
    /// <param name="Report">What to tell the user, in both languages (Claude reads the English in the status bar).</param>
    /// <param name="Reloaded">
    /// The world was read again (a save after zone resets or No limit ground; live, after zone resets only):
    /// areas must be read again too. Live No limit ground shows in <see cref="Lifted"/> alone, which callers
    /// check as well.
    /// </param>
    /// <param name="Saved">The writer's result (offline only).</param>
    public sealed partial record Outcome(bool Done, Said Report, bool Reloaded, WorldWriter.Result? Saved = null)
    {
        /// <summary>What to tell the user, in the language in use.</summary>
        public string Message => Report.Shown;

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
        // The apply lock is not disposed: an apply still under way releases it when it ends (its outcome would be
        // lost to an ObjectDisposedException), and it holds nothing to free (its wait handle is never asked for).
    }

    // ---- Live: following the game. The zones shown are asked about every few seconds (two numbers each,
    // cheap); those that changed are read again (FetchGame, on the network: whole when something that stays
    // put changed, only the kinds that move or tick on their own when only those did) and merged in
    // (MergeGame, at once on the window's thread): objects made, removed or changed in the game appear as
    // they are, the editor's own objects take the place and data the game gives them, and ground changed
    // there replaces the editor's where it has no changes of its own (where it has, they are kept, and
    // applying them asks first: Edits.PendingOverGame).
    /// <summary>Each zone's digests as last merged (the game's two numbers per zone); cleared when the world is read again.</summary>
    private readonly Dictionary<(int, int), LiveBridge.Digests> _digests = [];

    /// <summary>What <c>FetchGame</c> read from the game, for <c>MergeGame</c>.</summary>
    /// <param name="World">The world it was read for (dropped if the world was read again since).</param>
    /// <param name="Digests">Every zone asked about, with its digests now.</param>
    /// <param name="Changed">The zones read whole (their first digest changed): objects of them the game lacks vanish.</param>
    /// <param name="Objects">The objects read, as the game sends them (owner, id, data): the changed zones' and the moving ones.</param>
    /// <param name="Applies">Apply live's count when the read began (dropped if an apply began or ended since).</param>
    public sealed record Fetched(WorldSave World, Dictionary<(int X, int Z), LiveBridge.Digests> Digests, List<(int X, int Z)> Changed, List<(long User, uint Id, byte[] Bytes)> Objects, int Applies)
    {
        /// <summary>
        /// The zones whose kinds that move or tick on their own were read alone (only their second digest changed):
        /// their objects are in <see cref="Objects"/> too, and nothing of them vanishes.
        /// </summary>
        public List<(int X, int Z)> Moving { get; init; } = [];
    }

    /// <summary>What a merge of the game's changes did.</summary>
    /// <param name="Merged">The objects added, updated and vanished in the world.</param>
    /// <param name="Ours">The editor's own new objects the game removed (players picked or destroyed them).</param>
    /// <param name="Ground">Zones whose ground came in.</param>
    /// <param name="Kept">Zones whose ground the game changed under the editor's changes.</param>
    public sealed record Followed(WorldSave.Merged Merged, List<int> Ours, List<(int X, int Z)> Ground, List<(int X, int Z)> Kept)
    {
        /// <summary>
        /// The editor's own new objects that took the game's place, turn or data (the game moved them, or gave
        /// them data once made).
        /// </summary>
        public List<int> Refreshed { get; init; } = [];

        /// <summary>Anything changed at all.</summary>
        public bool Any => Merged.Added.Count + Merged.Updated.Count + Merged.Vanished.Count + Ours.Count + Refreshed.Count + Ground.Count + Kept.Count > 0;
    }
}
