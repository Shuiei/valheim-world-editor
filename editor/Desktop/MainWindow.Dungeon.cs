using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// Building in a dungeon (the Dungeon panel's Build here): the Workshop's Build (one piece at a time,
// where the hammer would put it, snapping) over the dungeon's rooms, whose floors and walls the cursor
// points at. Pieces resting on a room hold in game: its floor counts as ground for their support.
// And a dungeon into the world (generated, or a blueprint tagged as one): its entrance on the ground,
// the dungeon 5000 m above.
public partial class MainWindow
{
    /// <summary>
    /// The Place tool's settings from before Build here (restored when it ends); null when not building in a dungeon.
    /// </summary>
    private Kept? _dungeonKept;

    /// <summary>True while Build here is on.</summary>
    internal bool BuildingInDungeon => _dungeonKept != null;

    /// <summary>
    /// Writes a generated dungeon's blueprint file (path, text). Tests hold it to change the window meanwhile.
    /// </summary>
    internal Func<string, string, Task> WriteBlueprintFile { get; set; } = (path, text) => File.WriteAllTextAsync(path, text);

    /// <summary>
    /// Turns Build here on (the Place tool's settings are kept aside, the Workshop's Build starts and the Place
    /// tool cuts at the view's cut height) or off (the Place tool gets its settings back, its status line goes).
    /// </summary>
    internal void BuildInDungeon(bool on)
    {
        var t = PlaceTool;
        if (on && _dungeonKept == null)
        {
            _dungeonKept = KeepPlaceTool();
            BuildPanel.Start(library: false);
            t.CutY = View.CutY;
            Tools.ChooseMode(ToolMode.Place);
            Msg("dungeon.msg.building");
        }
        else if (!on && _dungeonKept is { } k)
        {
            _dungeonKept = null;
            RestorePlaceTool(k);
            t.Notify();
            // Its "Building in the dungeon" line no longer holds (another message since stays).
            if (LastSaid.Shown == Said.Of("dungeon.msg.building").Shown)
            {
                Msg(Said.Empty);
            }
        }
    }

    /// <summary>
    /// The dungeon last placed in this area (<see cref="PlaceGenerated"/>, <see cref="PasteDungeon"/>), to go up to and
    /// come back from; null before one is placed, and in another area.
    /// </summary>
    /// <param name="Entrance">Where its entrance stands on the ground (its portal, when it has no entrance).</param>
    /// <param name="Arrival">Where a player arrives in it, high up.</param>
    /// <param name="Cut">The roof cut that shows its first level.</param>
    /// <param name="Dungeon">Its dungeon object's place when it is one of the game's rooms (the Dungeon panel lists it), else null.</param>
    /// <param name="Step">The history's step that placed it.</param>
    /// <param name="Undone">That step is undone (on the redo list): nothing to go to until it is redone.</param>
    internal sealed record PlacedDungeon(Vector3 Entrance, Vector3 Arrival, float Cut, Vector3? Dungeon, EditSession.Change Step, bool Undone = false);

    /// <summary>
    /// The dungeon last placed in this area, or null; the Dungeon panel's Go to buttons follow, and its line for an area
    /// without a dungeon of rooms (one of pieces is none, yet the area has a dungeon).
    /// </summary>
    internal PlacedDungeon? LastPlaced
    {
        get;
        private set
        {
            field = value;
            FollowPlaced();
        }
    }

    /// <summary>
    /// The dungeon last placed follows the history (an undo, a redo, an edit after an undo): undone, the Go to buttons
    /// are off until it is redone; undone and then replaced by another edit, it is forgotten. The Dungeon panel's Go to
    /// buttons and its line for a dungeon of pieces follow.
    /// </summary>
    private void FollowPlaced()
    {
        if (LastPlaced is { } p && Session is { } s)
        {
            bool undone = s.RedoList.Any(c => ReferenceEquals(c, p.Step));
            if (p.Undone && !undone && !s.UndoList.Any(c => ReferenceEquals(c, p.Step)))
            {
                LastPlaced = null;
                return;
            }
            if (undone != p.Undone)
            {
                LastPlaced = p with { Undone = undone };
                return;
            }
        }
        bool there = LastPlaced is { Undone: false };
        DungeonPanel.Gen.Placed(there);
        DungeonPanel.ShowPiecesPlaced(there && LastPlaced!.Dungeon == null);
    }

    /// <summary>
    /// The view up to the dungeon last placed: looking down into its first level, its roofs cut away (one of the game's
    /// rooms: the Dungeon panel's own Go to the dungeon on it).
    /// </summary>
    internal void GoToDungeon()
    {
        if (LastPlaced is not { Undone: false } p)
        {
            return;
        }
        if (p.Dungeon is { } at && DungeonPanel.Select(at))
        {
            DungeonPanel.Inside();
            return;
        }
        View.Orbit(p.Arrival.X, p.Arrival.Z, 30, 55, 60, p.Arrival.Y);
        View.CutY = p.Cut;
    }

    /// <summary>The view back down to the entrance of the dungeon last placed, on the ground, nothing cut away.</summary>
    internal void GoToEntrance()
    {
        if (LastPlaced is not { Undone: false } p)
        {
            return;
        }
        DungeonPanel.ShowRoofs();
        View.Orbit(p.Entrance.X, p.Entrance.Z, View.Camera.Yaw * 180f / MathF.PI, 55, 40);
        View.CutY = null;
    }

    /// <summary>An object of a dungeon to place, in the dungeon's own frame.</summary>
    /// <param name="Prefab">Its prefab hash.</param>
    /// <param name="Position">Where it stands, from the dungeon's origin.</param>
    /// <param name="Rotation">Its turn.</param>
    /// <param name="Scale">Its scale (0: its prefab's own).</param>
    /// <param name="Data">What it holds (see ObjectField), null for nothing of its own.</param>
    /// <param name="Piece">A player-built piece (for the history).</param>
    private sealed record DungeonPiece(int Prefab, Vector3 Position, Quaternion Rotation, float Scale, IReadOnlyList<ObjectField>? Data, bool Piece);

    /// <summary>
    /// A dungeon to place (<see cref="PlaceDungeon"/>): its objects in its own frame, where a player arrives in it and
    /// the way they face, and what entrance it takes.
    /// </summary>
    /// <param name="Name">Its name, for the history and the message.</param>
    /// <param name="Pasted">Pasted from a blueprint, else generated: what the message and the history say was done.</param>
    /// <param name="Biome">The biome whose dungeon entrance it takes (when it is not one of the game's own dungeons).</param>
    /// <param name="PortalTag">The tag of its portals, when it gets a portal pair instead of an entrance.</param>
    /// <param name="Pieces">Its objects, from its origin.</param>
    /// <param name="Landing">Where a player arrives in it, from its origin.</param>
    /// <param name="Facing">The way a player arriving faces, degrees about the vertical (0: +z).</param>
    /// <param name="Rooms">
    /// One of the game's dungeons: makes its rooms and what is in them, laid out from its dungeon object at this pose
    /// (null: from the origin, facing +z). Null for a dungeon of pieces alone.
    /// </param>
    /// <param name="Label">The history's name for the step; null: "Dungeon:", what was done and the name.</param>
    private sealed record DungeonToPlace(string Name, bool Pasted, string Biome, string PortalTag, List<DungeonPiece> Pieces, Vector3 Landing, float Facing,
        Func<DungeonEntrances.Pose?, DungeonGen.Result>? Rooms, string? Label = null)
    {
        /// <summary>The history's name for the step: <see cref="Label"/>, else "Dungeon:", what was done and the name.</summary>
        public string Step => Label ?? (Pasted ? Lang.T("dungeon.undo.pasted", Name) : Lang.T("dungeon.undo.generated", Name));

        /// <summary>The first sentence of the message once placed: what was done, with how many objects.</summary>
        /// <param name="objects">How many objects were added.</param>
        public Said Done(int objects) => Pasted ? Said.Of("dungeon.msg.pasted", Name, objects) : Said.Of("dungeon.msg.generated", Name, objects);
    }

    /// <summary>The generator's objects as pieces to place: each with its data and no builder (a ruin, as the game's own dungeons).</summary>
    private static List<DungeonPiece> ToPieces(IEnumerable<DungeonGen.Item> items) => [.. items.Select(it =>
    {
        int prefab = StableHash.Of(it.Prefab);
        var data = it.Values().Append(ObjectField.NoBuilder).ToList();
        return new DungeonPiece(prefab, it.Position, it.Rotation, 0f, data, Terrain.PieceCatalog.Get(prefab)?.Tool != null);
    })];

    /// <summary>
    /// A generated dungeon into the world, above the ground at the view's centre (<see cref="PlaceDungeon"/>): a
    /// dungeon of the game's rooms is made again from where the entrance puts its dungeon object, as the game lays its
    /// own out from there.
    /// </summary>
    /// <param name="settings">The generator's settings.</param>
    /// <param name="r">The dungeon made from them.</param>
    /// <param name="label">The history's name for the step; null: "Dungeon: generated" and its name.</param>
    internal void PlaceGenerated(DungeonGen.Settings settings, DungeonGen.Result r, string? label = null)
    {
        if (View.Scene is not { Session: { } session } scene)
        {
            Msg("dungeon.msg.openArea");
            return;
        }
        var target = View.Camera.Target;
        var at = GroundAt(scene, target.X + scene.Cx, scene.Cz - target.Z);
        var world = scene.World;
        Func<DungeonEntrances.Pose?, DungeonGen.Result>? rooms = r.Rooms == null ? null : pose => pose is { } p ? DungeonGen.Make(settings, world.Seed, p) : r;
        PlaceDungeon(scene, session, at, new DungeonToPlace(r.Name, false, settings.Biome, PortalTag(settings.Seed), ToPieces(r.Items), r.Arrival, 0f, rooms, label));
    }
}
