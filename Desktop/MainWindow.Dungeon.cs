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
    /// tool cuts at the view's cut height) or off (the Place tool gets its settings back).
    /// </summary>
    internal void BuildInDungeon(bool on)
    {
        var t = PlaceTool;
        if (on && _dungeonKept == null)
        {
            _dungeonKept = KeepPlaceTool();
            BuildPanel.Start();
            t.CutY = View.CutY;
            Tools.ChooseMode(ToolMode.Place);
            MessageText.Text = "Building in the dungeon: pick a piece, then click on a room's floor or on a piece. Pieces on a room's floor hold in game.";
        }
        else if (!on && _dungeonKept is { } k)
        {
            _dungeonKept = null;
            RestorePlaceTool(k);
            t.Notify();
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
    internal sealed record PlacedDungeon(Vector3 Entrance, Vector3 Arrival, float Cut, Vector3? Dungeon);

    /// <summary>The dungeon last placed in this area, or null; the Dungeon panel's Go to buttons follow.</summary>
    internal PlacedDungeon? LastPlaced
    {
        get;
        private set
        {
            field = value;
            DungeonPanel.Gen.Placed(value != null);
        }
    }

    /// <summary>
    /// The view up to the dungeon last placed: looking down into its first level, its roofs cut away (one of the game's
    /// rooms: the Dungeon panel's Go inside on it).
    /// </summary>
    internal void GoToDungeon()
    {
        if (LastPlaced is not { } p)
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
        if (LastPlaced is not { } p)
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
    /// <param name="Verb">What was done, for the message ("Generated", "Pasted").</param>
    /// <param name="Biome">The biome whose dungeon entrance it takes (when it is not one of the game's own dungeons).</param>
    /// <param name="PortalTag">The tag of its portals, when it gets a portal pair instead of an entrance.</param>
    /// <param name="Pieces">Its objects, from its origin.</param>
    /// <param name="Landing">Where a player arrives in it, from its origin.</param>
    /// <param name="Facing">The way a player arriving faces, degrees about the vertical (0: +z).</param>
    /// <param name="Rooms">
    /// One of the game's dungeons: makes its rooms and what is in them, laid out from its dungeon object at this pose
    /// (null: from the origin, facing +z). Null for a dungeon of pieces alone.
    /// </param>
    /// <param name="Label">The history's name for the step; null: "Dungeon:", the verb and the name.</param>
    private sealed record DungeonToPlace(string Name, string Verb, string Biome, string PortalTag, List<DungeonPiece> Pieces, Vector3 Landing, float Facing,
        Func<DungeonEntrances.Pose?, DungeonGen.Result>? Rooms, string? Label = null);

    /// <summary>The generator's objects as pieces to place: each with its data and no builder (a ruin, as the game's own dungeons).</summary>
    private static List<DungeonPiece> ToPieces(IEnumerable<DungeonGen.Item> items) => [.. items.Select(it =>
    {
        int prefab = StableHash.Of(it.Prefab);
        var data = (it.Data ?? []).Select(d => new ObjectField(d.Section, StableHash.Of(d.Key), d.Value)).Append(ObjectField.NoBuilder).ToList();
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
            MessageText.Text = "Open an area of a world first: the dungeon goes above the middle of the view.";
            return;
        }
        var target = View.Camera.Target;
        var at = GroundAt(scene, target.X + scene.Cx, scene.Cz - target.Z);
        var world = scene.World;
        Func<DungeonEntrances.Pose?, DungeonGen.Result>? rooms = r.Rooms == null ? null : pose => pose is { } p ? DungeonGen.Make(settings, world.Seed, p) : r;
        PlaceDungeon(scene, session, at, new DungeonToPlace(r.Name, "Generated", settings.Biome, PortalTag(settings.Seed), ToPieces(r.Items), r.Arrival, 0f, rooms, label));
    }

    /// <summary>
    /// A dungeon blueprint pasted (the Paste tool's click, the clipboard's copy tagged as a dungeon): its pieces into the
    /// world above the ground at the grid point clicked, behind the entrance of the biome there (<see cref="PlaceDungeon"/>),
    /// turned and mirrored as the Paste tool shows them (its height offset does not apply: the dungeon's height is the
    /// game's). The first portal among its pieces marks where a player arrives, facing its way, and is left out (the
    /// entrance's door out stands there); without one, a player arrives at the blueprint's middle, facing north.
    /// </summary>
    /// <param name="clip">The copy on the clipboard.</param>
    /// <param name="at">The grid point clicked.</param>
    internal void PasteDungeon(CopyData clip, Vector2 at)
    {
        if (Session is not { } s || View.Scene is not { } scene)
        {
            return;
        }
        float ox = s.Scene.X0 * 64f - 32f, oz = s.Scene.Z0 * 64f - 32f;
        var ground = GroundAt(scene, ox + at.X, oz + at.Y);
        // Turned and mirrored as the Paste tool pastes (PasteTool.Apply).
        var paste = View.Paste;
        Vector3 Place(CopyData.Obj o)
        {
            var p = paste.Xf(new Vector2(o.Dx, o.Dz));
            return new Vector3(p.X, o.Dy, p.Y);
        }
        int markerAt = clip.Objects.FindIndex(o => o.Name == "portal_wood");
        var marker = markerAt >= 0 ? clip.Objects[markerAt] : null;
        var pieces = clip.Objects.Where((o, i) => i != markerAt)
            .Select(o => new DungeonPiece(o.Prefab, Place(o), BlueprintFormats.FromEuler(paste.Rotated(o.Rotation)), o.Scale, o.Data, Terrain.PieceCatalog.Get(o.Prefab)?.Tool != null)).ToList();
        // Counted as placing counts them: only what this world can make.
        if (!pieces.Exists(p => CanMake(scene.World, p.Prefab)))
        {
            MessageText.Text = pieces.Count == 0 && marker != null ? "Nothing to paste: the blueprint holds only its portal (where players arrive), no piece."
                : "Nothing to paste: the blueprint has no piece this world can have.";
            return;
        }
        string biome = BiomeName(scene.Terrain?.BiomeAt(ground.X, ground.Z));
        string name = clip.Name ?? "dungeon";
        PlaceDungeon(scene, s, ground, new DungeonToPlace(name, "Pasted", biome, PortalTag(StableHash.Of(name)), pieces,
            marker == null ? Vector3.Zero : Place(marker), marker == null ? 0f : paste.Rotated(marker.Rotation).Y, null));
    }

    /// <summary>Whether this world can make a prefab (its model or its details are known): placing leaves out the rest.</summary>
    private static bool CanMake(WorldSave world, int prefab) => world.ModelFor(prefab, null) != null || Terrain.PrefabCatalog.Details(prefab) != null;

    /// <summary>The biome's name as the dungeon generator knows it (the Black Forest's for none, or the sea's).</summary>
    private static string BiomeName(ValheimGen.Heightmap.Biome? biome) => biome switch
    {
        ValheimGen.Heightmap.Biome.Meadows => "Meadows",
        ValheimGen.Heightmap.Biome.Swamp => "Swamp",
        ValheimGen.Heightmap.Biome.Mountain => "Mountain",
        ValheimGen.Heightmap.Biome.Plains => "Plains",
        ValheimGen.Heightmap.Biome.Mistlands => "Mistlands",
        ValheimGen.Heightmap.Biome.AshLands => "Ashlands",
        ValheimGen.Heightmap.Biome.DeepNorth => "Deep North",
        _ => "Black Forest",
    };

    /// <summary>A point of the area on the ground (or the water): world x, z at the ground's height there.</summary>
    private static Vector3 GroundAt(WorldScene scene, float x, float z)
    {
        int i = Math.Clamp((int)MathF.Round(x - scene.Cx + (scene.W - 1) / 2f), 0, scene.W - 1), j = Math.Clamp((int)MathF.Round(z - scene.Cz + (scene.H - 1) / 2f), 0, scene.H - 1);
        return new Vector3(x, Math.Max(scene.Heights[j * scene.W + i], scene.Water), z);
    }

    /// <summary>
    /// A dungeon into the world, 5000 m above a point of the ground (the game builds dungeons there, and treats what is
    /// above 3000 m as indoors). On the ground there, facing the camera, the entrance of its kind (the dungeon location
    /// of its biome, or of its own kind of game dungeon: a burial chamber's mound, a Frost Cave's mouth...), whose door
    /// leads up into it and whose door up there leads back out: the dungeon is laid out where the game lays that
    /// entrance's own, turned so that a player coming through the door out arrives where the dungeon has them arrive,
    /// facing its way. When the game's files describe no such entrance, a portal on the ground and its twin where the
    /// player arrives, linked. The view stays at the entrance (<see cref="GoToDungeon"/> goes up,
    /// <see cref="GoToEntrance"/> comes back). One step of the history: Ctrl+Z takes it all back.
    /// </summary>
    /// <param name="scene">The area.</param>
    /// <param name="session">Its edits.</param>
    /// <param name="at">The point of the ground the entrance stands on.</param>
    /// <param name="d">The dungeon.</param>
    private void PlaceDungeon(WorldScene scene, EditSession session, Vector3 at, DungeonToPlace d)
    {
        try
        {
            LayDungeon(scene, session, at, d);
        }
        // The objects' data is read from the world's files (NewObjectBytes): one gone or locked meanwhile places nothing.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageText.Text = $"{d.Name} was not placed: the world's files could not be read ({ex.Message}).";
        }
    }

    /// <summary><see cref="PlaceDungeon"/>'s work; the world's files read failing throws.</summary>
    /// <param name="scene">The area.</param>
    /// <param name="session">Its edits.</param>
    /// <param name="at">The point of the ground the entrance stands on.</param>
    /// <param name="d">The dungeon.</param>
    private void LayDungeon(WorldScene scene, EditSession session, Vector3 at, DungeonToPlace d)
    {
        var world = scene.World;
        var (camYaw, _, _, _) = View.Camera;
        // What is taken high up: dungeons laid out, objects, and over the dungeons' locations near here, the space the
        // game will lay theirs out in.
        List<(int, Vector3)> high;
        lock (scene.Things)
        {
            high = [.. scene.Things.Where(t => !t.Gone && t.Position.Y > 3000).Select(t => (t.Prefab, t.Position))];
        }
        var near = world.Locations.Where(l => MathF.Abs(l.Position.X - at.X) < 600 && MathF.Abs(l.Position.Z - at.Z) < 600);
        var taken = DungeonGen.Taken(high, near);
        Func<ObjectRef, byte[]> source = world.SourceReader();
        byte[]? Blank(NewObject n) => world.NewObjectBytes(n, source);
        bool Known(int prefab) => CanMake(world, prefab);
        var adds = new List<(NewObject, bool)>();
        // The entrance of its kind, when the game's files describe one with a door out; its front toward the camera.
        var kindOf = d.Rooms?.Invoke(null).Kind;
        var entrance = kindOf != null ? DungeonEntrances.For(kindOf.Name) : DungeonEntrances.ForBiome(d.Biome);
        var toward = at + new Vector3(MathF.Sin(camYaw), 0, -MathF.Cos(camYaw)) * 10;
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (entrance == null ? 0 : DungeonEntrances.YawToFace(entrance, at, toward)) * MathF.PI / 180f);
        var door = entrance == null ? null : DungeonEntrances.ExitPose(entrance, at, turn);
        var dungeonPose = entrance == null ? null : DungeonEntrances.DungeonPose(entrance, at, turn);
        bool viaEntrance = entrance != null && door != null && (d.Rooms == null || dungeonPose != null);
        var pieces = d.Pieces;
        List<Dungeons.Placed>? rooms = null;
        Dungeons.Kind? kind = null;
        // Where the dungeon's own frame stands in the world (its origin), and where a player arrives.
        var origin = new Vector3(MathF.Round(at.X), MathF.Round(at.Y) + 5000, MathF.Round(at.Z));
        Vector3 arrival;
        float portalYaw = d.Facing;
        if (d.Rooms != null)
        {
            // The game's rooms: laid out from the dungeon object where the entrance puts it, else from the origin.
            var r = d.Rooms(viaEntrance ? dungeonPose : null);
            kind = r.Kind;
            rooms = r.Rooms;
            pieces = ToPieces(r.Items);
            origin = viaEntrance ? Vector3.Zero : DungeonGen.Clear(origin, r, taken);
            arrival = origin + r.Arrival;
            portalYaw = 0f;
        }
        else if (viaEntrance)
        {
            // The pieces turned and moved so that the door out opens where a player arrives, facing their way.
            var landing = DungeonEntrances.Landing(door!.Value);
            var spin = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (DungeonEntrances.YawOf(door.Value) - d.Facing) * MathF.PI / 180f);
            pieces = [.. pieces.Select(p => p with { Position = landing + Vector3.Transform(p.Position - d.Landing, spin), Rotation = Quaternion.Normalize(spin * p.Rotation) })];
            origin = Vector3.Zero;
            arrival = landing;
        }
        else
        {
            origin = DungeonGen.Clear(origin, [.. pieces.Select(p => p.Position)], 10, taken);
            arrival = origin + d.Landing;
        }
        string way;
        if (viaEntrance)
        {
            // The game lays one dungeon out per zone, in its box: another there already would share its space.
            var points = pieces.Select(p => p.Position + origin).Concat(rooms?.Select(p => p.Position + origin) ?? []).ToList();
            var box = points.Count == 0 ? new DungeonGen.Box(arrival, arrival) : new DungeonGen.Box(points.Aggregate(Vector3.Min) - new Vector3(10, 6, 10), points.Aggregate(Vector3.Max) + new Vector3(10, 6, 10));
            if (taken.Any(t => t.Overlaps(box)))
            {
                MessageText.Text = "Something is already up there (a dungeon, or the space the game keeps for one): put the entrance in another zone (64 × 64 m).";
                return;
            }
            // The entrance: its location proxy (the game makes its door, its stones and its interior from it whenever
            // the zone loads) and the objects it makes, all but its own dungeon object (the dungeon placed is the one).
            int zoneSeed = LocationPrefabs.ZoneSeed(world.Seed, (int)MathF.Floor((at.X + 32f) / 64f), (int)MathF.Floor((at.Z + 32f) / 64f));
            ushort flags = Terrain.PrefabCatalog.Get(WorldSave.LocationProxyPrefab)?.Flags ?? 0x0B00;
            var euler = BlueprintFormats.ToEuler(turn);
            adds.Add((new NewObject(0, WorldSave.LocationProxyPrefab, at, euler, 0, Fresh: false,
                Raw: ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, flags, at, euler, 0f, [(WorldSave.LocationKey, entrance!.Hash), (StableHash.Of("seed"), zoneSeed)])), false));
            foreach (var m in LocationPrefabs.Made(entrance, at, turn, zoneSeed))
            {
                int prefab = StableHash.Of(m.Prefab);
                if (Dungeons.KindOf(prefab) == null && !m.Prefab.StartsWith("DG_", StringComparison.Ordinal) && Known(prefab))
                {
                    adds.Add((new NewObject(0, prefab, m.Position, BlueprintFormats.ToEuler(m.Rotation), 0), false));
                }
            }
            way = $"Its {DungeonEntrances.Describe(entrance)} entrance stands here, facing you.";
        }
        else
        {
            way = $"A portal “{d.PortalTag}” on the ground here leads in.";
        }
        Vector3? dungeonAt = null;
        if (rooms != null && kind != null)
        {
            // The game's rooms: a dungeon object holding the list, which the game builds them from.
            var placed = rooms.Select(p => p with { Position = p.Position + origin }).ToList();
            if (placed.Count == 0)
            {
                MessageText.Text = $"{d.Name} has no rooms: nothing placed.";
                return;
            }
            var pose = viaEntrance ? dungeonPose!.Value : new DungeonEntrances.Pose(origin, Quaternion.Identity);
            var dg = new NewObject(0, kind.Hash, pose.Position, BlueprintFormats.ToEuler(pose.Rotation), 0);
            if (Blank(dg) is not { } bytes)
            {
                MessageText.Text = $"{kind.Name} cannot be made in this world.";
                return;
            }
            var z = ZdoData.Parse(bytes);
            z.SetBytes(Dungeons.RoomDataKey, Dungeons.Write(placed));
            adds.Add((dg with { Raw = z.Serialize(), Fresh = false }, false));
            dungeonAt = pose.Position;
            // Arriving on the entrance room's floor, found under its middle.
            var dungeon = new DungeonRooms.Dungeon(-1, new WorldScene.Thing(-1, kind.Hash, pose.Position, Vector3.Zero, 1, false), kind, placed, null);
            var above = placed[0].Position + new Vector3(0, 3, 0);
            arrival = RoomSurfaces.Hit([dungeon], View.Models, above, -Vector3.UnitY) is float down ? above - new Vector3(0, down - 0.05f, 0) : placed[0].Position;
        }
        // Every object with its own data. Made fresh (nothing kept of the object it is copied from); the data goes on
        // when it is written (a generated dungeon's: no builder, so the game takes it for a ruin, as its own dungeons).
        int skipped = 0;
        foreach (var p in pieces)
        {
            if (!Known(p.Prefab))
            {
                skipped++;
                continue;
            }
            adds.Add((new NewObject(0, p.Prefab, p.Position + origin, BlueprintFormats.ToEuler(p.Rotation), p.Scale, Data: p.Data), p.Piece));
        }
        if (!viaEntrance)
        {
            // Two portals of the same tag, written linked (the game pairs only portals it sees made, or that
            // a save gives linked).
            int link = Random.Shared.Next(1, int.MaxValue);
            int portal = StableHash.Of("portal_wood");
            var portalData = new[] { ObjectField.NoBuilder, new ObjectField("strings", StableHash.Of("tag"), d.PortalTag) };
            foreach (var (where, yaw, kindByte) in new[] { (arrival, portalYaw, (byte)0x01), (GroundAt(scene, at.X, at.Z + 3), 180f, (byte)0x11) })
            {
                var n = new NewObject(0, portal, where, new Vector3(0, yaw, 0), 0);
                if (Blank(n) is not { } bytes)
                {
                    continue;
                }
                // The link is no field of the data: set on the object itself.
                var z = ZdoData.Parse(bytes);
                z.Connection = [kindByte, .. BitConverter.GetBytes(link)];
                adds.Add((n with { Raw = z.Serialize(), Fresh = false, Data = portalData }, true));
            }
        }
        session.Commit(d.Label ?? $"Dungeon: {d.Verb.ToLowerInvariant()} {d.Name}", null, [], adds);
        LastPlaced = new PlacedDungeon(at, arrival, arrival.Y + 3.5f, dungeonAt);
        MessageText.Text = $"{d.Verb} {d.Name}: {adds.Count} objects, 5000 m above here. {way} "
            + (skipped > 0 ? $"{skipped} could not be made in this world. " : "") + "Go to the dungeon (Dungeon panel) takes the view up there, Go to the entrance back. Ctrl+Z takes it back; Save writes it.";
        // The view stays on the ground, looking at the entrance.
        View.Orbit(at.X, at.Z, camYaw * 180f / MathF.PI, 55, 40);
        View.CutY = null;
        UpdateSaveBar();
    }

    /// <summary>
    /// The tag of a dungeon's portal pair from a number (its seed, its name's hash): "dg" and the last five digits of
    /// its size, never negative (a negative seed) nor failing (a hash of int.MinValue has no int size).
    /// </summary>
    internal static string PortalTag(long n) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"dg{Math.Abs(n) % 100000}");

    /// <summary>The tags a generated dungeon's blueprint is written with: a dungeon (pasted, it goes up behind an entrance), generated.</summary>
    private static readonly string[] DungeonTags = [Homestead.Details.DungeonTag, "generated"];

    /// <summary>
    /// A generated dungeon as a blueprint (in the game's Homestead folder), opened in the Workshop. What
    /// its objects hold (the key in its chest, signs, the boss's stars) goes in the editor's own lines of
    /// the file, and the building is written as a ruin (no builder): the editor puts them back when the
    /// blueprint is placed in a world. Built in game with Homestead, it is only the objects.
    /// </summary>
    internal async Task OpenGeneratedInWorkshop(DungeonGen.Settings settings)
    {
        if (await WriteGeneratedBlueprint(settings, DungeonGen.Make(settings)) is string path)
        {
            await OpenWorkshop(path);
        }
    }

    /// <summary>
    /// The Workshop's Generate dungeon: the dungeon made onto the plot, alone (asking first when what is
    /// there is not saved) or added to it in its middle (not when the plot changed while its blueprint was
    /// written: said). Its blueprint stays in the Library.
    /// </summary>
    internal async Task GeneratedOntoPlot(DungeonGen.Settings settings, DungeonGen.Result r, bool add)
    {
        // The plot it is added to: another one opened (or the Workshop left) while the blueprint was written does not
        // get it.
        var plot = Session;
        if (await WriteGeneratedBlueprint(settings, r) is not string path)
        {
            return;
        }
        BuildPanel.RenderLibrary();
        if (add)
        {
            if (!InWorkshop || Session != plot)
            {
                MessageText.Text = "The Workshop changed while the dungeon was written: nothing placed. Its blueprint is in the Library.";
                return;
            }
            AddToWorkshop(path, null);
        }
        else
        {
            await OpenWorkshop(path);
        }
    }

    /// <summary>
    /// A generated dungeon written as a blueprint, tagged a dungeon, with a portal where a player arrives in it (its
    /// mark: pasted into a world, the entrance's door out opens there); its file, or null when it could not be written
    /// (said).
    /// </summary>
    private async Task<string?> WriteGeneratedBlueprint(DungeonGen.Settings settings, DungeonGen.Result r)
    {
        try
        {
            // The Library's folder (Homestead's).
            string folder = Blueprints.Status.Folder;
            Directory.CreateDirectory(folder);
            // A blueprint of that name already (one changed in the Workshop, perhaps): kept, this one numbered.
            string path = Path.Combine(folder, Homestead.FileName(Homestead.FreeName(r.Name, n => File.Exists(Path.Combine(folder, Homestead.FileName(n))))));
            await WriteBlueprintFile(path, GeneratedBlueprint(settings, r));
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageText.Text = $"Could not write the dungeon's blueprint: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Claude's generate_dungeon in the Workshop: the dungeon added to what is on the plot, in its middle, as one step
    /// named <paramref name="label"/>. Its blueprint is not kept: the Library is the user's (Claude never writes it).
    /// </summary>
    internal void AddGeneratedToPlot(DungeonGen.Settings settings, DungeonGen.Result r, string label) =>
        AddToWorkshop(Homestead.FileName(r.Name), null, label: label, text: GeneratedBlueprint(settings, r));

    /// <summary>
    /// A generated dungeon as a Homestead blueprint's text (what <see cref="WriteGeneratedBlueprint"/> writes).
    /// </summary>
    private static string GeneratedBlueprint(DungeonGen.Settings settings, DungeonGen.Result r)
    {
        var items = r.Items.Append(new DungeonGen.Item("portal_wood", r.Arrival, Quaternion.Identity)).ToList();
        float mx = (items.Min(i => i.Position.X) + items.Max(i => i.Position.X)) / 2, mz = (items.Min(i => i.Position.Z) + items.Max(i => i.Position.Z)) / 2;
        float my = items.Min(i => i.Position.Y);
        var objects = new System.Text.Json.Nodes.JsonArray();
        foreach (var it in items)
        {
            var e = BlueprintFormats.ToEuler(it.Rotation);
            var data = (it.Data ?? [])
                .Select(d => new ObjectField(d.Section, StableHash.Of(d.Key), d.Value)).Append(ObjectField.NoBuilder);
            objects.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["name"] = it.Prefab,
                ["dx"] = it.Position.X - mx,
                ["dy"] = it.Position.Y - my,
                ["dz"] = it.Position.Z - mz,
                ["rx"] = e.X,
                ["ry"] = e.Y,
                ["rz"] = e.Z,
                ["scale"] = 0,
                ["data"] = BlueprintFormats.DataJson(data),
            });
        }
        return Homestead.Write(new System.Text.Json.Nodes.JsonObject { ["objects"] = objects }, r.Name, "Valheim World Editor", null, DateTime.Now,
            $"A generated dungeon (seed {settings.Seed}): {string.Join(" ", r.Notes).TrimEnd('.')}. The portal marks where players arrive: the entrance's door out opens there.", DungeonTags);
    }
}
