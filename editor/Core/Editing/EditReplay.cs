using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

/// <summary>What the game changed too, of what the editor changed (an <see cref="EditReplay"/> clash).</summary>
public enum ReplayClash
{
    /// <summary>A zone whose ground both changed.</summary>
    Ground,
    /// <summary>An object whose data the game changed, which the editor deleted, moved or edited.</summary>
    Changed,
    /// <summary>An object the game removed (or moved away), which the editor moved or edited.</summary>
    Removed,
}

/// <summary>
/// The pending edits of an open world brought onto a newer save of it, one the game made after the editor read the
/// world (Valheim played between two saves). Every pending change is made again on that save: the changed zones'
/// ground (heights, paint, smoothing, No limit ground), deleted objects, added and moved objects with their data, and
/// zone resets. Ids change at every save, so objects are found again by kind and place (<see cref="ObjectPlaces"/>).
/// What the game changed too is listed (<see cref="Clashes"/>), and <see cref="Apply"/> is told, per kind, whose
/// version stays. Nothing is written to disk: the edits wait in the store, to be saved as usual.
/// </summary>
/// <remarks>
/// The edits are taken against the save the world was read from, as WorldSession.Save writes them. When the editor
/// saved during the session, the game's save may be made from the editor's save (then what it wrote is found there
/// already, and is not made twice) or from the save the game had in memory (then it is made again).
/// </remarks>
public sealed partial class EditReplay
{
    /// <summary>What the editor's own saves of the session wrote, as WorldSession keeps it (empty before any save).</summary>
    /// <param name="Zones">The zones it wrote ground for.</param>
    /// <param name="Deleted">The deletions as of its last save.</param>
    /// <param name="Added">The added objects as of its last save.</param>
    public sealed record Saved(IReadOnlySet<(int X, int Z)> Zones, IReadOnlySet<int> Deleted, IReadOnlySet<NewObject> Added)
    {
        /// <summary>No save made in the session.</summary>
        public static Saved None { get; } = new(new HashSet<(int X, int Z)>(), new HashSet<int>(), new HashSet<NewObject>());
    }

    /// <summary>Something the game changed too, of what the editor changed.</summary>
    /// <param name="Kind">What kind of clash it is: the user chooses per kind.</param>
    /// <param name="Prefab">The object's prefab hash (0 for a zone).</param>
    /// <param name="Position">Where the object stood when the editor read the world (a zone: its middle).</param>
    /// <param name="Unsure">
    /// The object's data as the editor read it could not be read again (the game removed that save): the game may not
    /// have changed it.
    /// </param>
    public sealed record Clash(ReplayClash Kind, int Prefab, Vector3 Position, bool Unsure)
    {
        /// <summary>The 64 m zone it is in.</summary>
        public (int X, int Z) Zone => ((int)MathF.Floor((Position.X + 32f) / 64f), (int)MathF.Floor((Position.Z + 32f) / 64f));
    }

    /// <summary>What <see cref="Apply"/> did.</summary>
    /// <param name="Zones">Zones whose ground is pending now.</param>
    /// <param name="Deleted">Objects of the game's save to delete.</param>
    /// <param name="Added">Objects to add (moved ones included).</param>
    /// <param name="Resets">Zones to reset.</param>
    /// <param name="Dropped">
    /// The editor's objects whose change was dropped for the game's version: ids of the save the editor read, and the
    /// added objects' own (negative) ids. A step of the history that touches one cannot be undone any more.
    /// </param>
    /// <param name="GameGround">The zones whose ground stays as the game has it (the editor's changes there dropped).</param>
    /// <param name="Unread">
    /// Objects whose data, as the editor read them, could not be read again (the game removed that save): compared as
    /// maybe changed, or brought back as new objects of their kind without their data.
    /// </param>
    public sealed record Replayed(int Zones, int Deleted, int Added, int Resets, IReadOnlySet<int> Dropped, IReadOnlySet<(int X, int Z)> GameGround, int Unread);

    /// <summary>An object change of the editor's that clashes with the game's: kept or dropped as the user chooses.</summary>
    /// <param name="Kind">Its kind.</param>
    /// <param name="Old">
    /// The object as the editor knew it: its id in the save read, or the own (negative) id of one the editor's save wrote.
    /// </param>
    /// <param name="Newer">The object in the game's save (null: the game has it no more).</param>
    /// <param name="Mine">
    /// The objects added when the editor's change is kept (a move's copy at its new place, an edited copy in its place).
    /// </param>
    /// <param name="Original">The editor's objects as they were (kept as gone objects when the game's version stays).</param>
    private sealed record Item(ReplayClash Kind, int Old, int? Newer, List<NewObject> Mine, List<NewObject> Original);

    /// <summary>The save the world was read from, which the edits are made against.</summary>
    private readonly WorldSave _old;

    /// <summary>The edits' version when they were taken: changed since, <see cref="Apply"/> refuses (-1 once applied).</summary>
    private int _version;

    /// <summary>The game's save's objects by kind and place, each taken once found.</summary>
    private readonly ObjectPlaces _places;

    /// <summary>
    /// Objects found in the game's save (null: not there), by their id in the save the editor read, or by their own
    /// (negative) id for objects the editor's save wrote.
    /// </summary>
    private readonly Dictionary<int, int?> _found = [];

    /// <summary>Objects deleted then undone after the editor's save, brought back: their id in the save read, their new one.</summary>
    private readonly Dictionary<int, int> _back = [];

    /// <summary>The added objects as of the editor's last save, by id.</summary>
    private readonly Dictionary<int, NewObject> _savedAdds = [];

    /// <summary>Reads the chunk files of the save read, each once.</summary>
    private readonly Func<ObjectRef, byte[]> _oldSource;

    /// <summary>Reads the chunk files of the game's save, each once.</summary>
    private readonly Func<ObjectRef, byte[]> _newSource;

    /// <summary>The data of objects of the save read, as written now (null: could not be read), by id.</summary>
    private readonly Dictionary<int, byte[]?> _oldData = [];

    /// <summary>Objects of the save read whose data could not be read again.</summary>
    private readonly HashSet<int> _unread = [];

    /// <summary>The zones taken as they are (no clash).</summary>
    private readonly List<ZoneEdit> _zones = [];

    /// <summary>The zones both changed: the editor's, as read, as the editor's save wrote it (else as read), as the game has it.</summary>
    private readonly List<(ZoneEdit Mine, ZoneEdit Read, ZoneEdit Last, ZoneEdit Game)> _zoneClashes = [];

    /// <summary>Objects of the game's save to delete whatever is chosen.</summary>
    private readonly List<int> _deletes = [];

    /// <summary>Objects to add whatever is chosen.</summary>
    private readonly List<NewObject> _adds = [];

    /// <summary>The objects added and deleted again (undo can bring them back), fitted to the game's save.</summary>
    private readonly List<NewObject> _gone = [];

    /// <summary>Deletions undone after the editor's save that the game's save lacks: brought back (ids in the save read).</summary>
    private readonly List<int> _comeBack = [];

    /// <summary>The object changes that clash.</summary>
    private readonly List<Item> _items = [];

    /// <summary>The zone resets.</summary>
    private readonly List<ZoneReset> _resets = [];

    /// <summary>Every clash: the zones', then the objects'.</summary>
    private readonly List<Clash> _clashes = [];

    /// <summary>The game's save the edits are brought onto.</summary>
    public WorldSave Newer { get; }

    /// <summary>What the game changed too, of what the editor changed: zones first, then objects.</summary>
    public IReadOnlyList<Clash> Clashes => _clashes;

    /// <summary>Starts a replay (see <see cref="Prepare"/>).</summary>
    private EditReplay(WorldSave old, WorldSave newer, int version)
    {
        _old = old;
        Newer = newer;
        _version = version;
        _places = ObjectPlaces.Of(newer);
        _oldSource = old.SourceReader();
        _newSource = newer.SourceReader();
    }
}
