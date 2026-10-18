using System.Numerics;

namespace TerrainEditor.Save;

/// <summary>
/// An object in the running game: its ZDOID, and what it is and where, to find it again when another mod has made it
/// again under a new ZDOID.
/// </summary>
/// <param name="User">The ZDOID's user (owner peer) part.</param>
/// <param name="Id">The ZDOID's id part.</param>
/// <param name="Prefab">The object's prefab name hash.</param>
/// <param name="Position">The object's world position, in metres.</param>
public readonly record struct LiveTarget(long User, uint Id, int Prefab, Vector3 Position);

/// <summary>
/// An object to move in the running game under its own ZDOID, so what mods know of it (by its ZDO) holds.
/// </summary>
/// <param name="From">The object, where it stands now.</param>
/// <param name="Position">Its new place, in metres.</param>
/// <param name="Rotation">Its new turn, Unity Euler angles in degrees.</param>
/// <param name="Fallback">
/// The object at its new place in the save format: what the plugin makes instead (destroying the object) when a player
/// has both places loaded, whose game would not show it moving.
/// </param>
public readonly record struct LiveMove(LiveTarget From, Vector3 Position, Vector3 Rotation, byte[] Fallback);

/// <summary>
/// Keeps the running game's objects in step with the editor's deletions and new objects. It remembers what has already
/// been applied, so each apply sends only the difference, and undo works after applying: a deleted object is created
/// again from its snapshot bytes, a new one is destroyed.
/// </summary>
public sealed partial class LiveSync
{
    /// <summary>
    /// Guards the collections below and the unanswered call; Apply records the game's answer after an await, possibly on
    /// another thread.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>Snapshot objects (ids &gt;= 0) currently destroyed in the game.</summary>
    private readonly HashSet<int> _destroyed = [];

    /// <summary>Objects whose live ZDOID is not the snapshot's: new objects (ids &lt; 0) and restored ones.</summary>
    private readonly Dictionary<int, LiveTarget> _liveIds = [];

    /// <summary>
    /// The ZDOIDs in <see cref="_liveIds"/>, each with how many ids stand for it, so <see cref="IsOurs"/> needs no scan.
    /// Changed only with <see cref="SetLive"/>, <see cref="DropLive"/> and <see cref="Reset"/>.
    /// </summary>
    private readonly Dictionary<(long User, uint Id), int> _ours = [];
}
