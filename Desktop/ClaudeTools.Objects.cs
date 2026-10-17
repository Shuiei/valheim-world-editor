using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's tools for objects already there: moving, turning and sizing them, replacing them with another kind, and
/// reading and changing what they hold in the save (the inspector's values). Each change replaces the objects by
/// copies (new ids), as the Select tool and the inspector do, in one pending step.
/// </summary>
public sealed partial class ClaudeTools
{
    // ---- Objects already there.

    /// <summary>The longest text value get_object gives whole; longer ones are cut.</summary>
    private const int LongestText = 2000;

    /// <summary>The smallest and largest scale set_transform takes.</summary>
    private const float MinScale = 0.05f, MaxScale = 20f;

    /// <summary>The open area's bounds in world metres.</summary>
    internal static (float MinX, float MinZ, float MaxX, float MaxZ) Bounds(WorldScene scene)
    {
        float minX = scene.OriginX, minZ = scene.OriginZ;
        return (minX, minZ, minX + scene.ZonesX * 64f, minZ + scene.ZonesZ * 64f);
    }

    /// <summary>
    /// The things of these ids that can be moved or changed (indices into the scene's things, each once), and why the
    /// others cannot: not an object of the area, or a runestone (the game builds it from its location).
    /// </summary>
    internal static (List<int> Ok, List<string> Problems) Usable(WorldScene scene, IEnumerable<int> ids)
    {
        var ok = new List<int>();
        var problems = new List<string>();
        foreach (int id in ids.Distinct())
        {
            if (id < 0 || id >= scene.Things.Count || scene.Things[id].Gone)
            {
                problems.Add($"{id}: not an object of the area");
            }
            else if (scene.Things[id].Runestone)
            {
                problems.Add($"{id}: a runestone, which the game builds from its location: it can only be taken away");
            }
            else
            {
                ok.Add(id);
            }
        }
        return (ok, problems);
    }

    /// <summary>
    /// The scene whose things the ids last given were indices into, and its read count then (list_objects,
    /// get_selection, place_pieces, each change's new ids...). Null until ids were given.
    /// </summary>
    private (WorldScene Scene, int Reads)? _ids;

    /// <summary>
    /// Notes that the ids given now are indices into this scene's things as they were at that read count, taken before
    /// the ids were worked out: a save that reads the world again meanwhile (on another thread) leaves them refused.
    /// </summary>
    /// <param name="scene">The scene the ids are of.</param>
    /// <param name="reads">Its read count before the ids were worked out.</param>
    private void GaveIds(WorldScene scene, int reads) => _ids = (scene, reads);

    /// <summary>
    /// Why the ids given before cannot be used on this scene: they were given for another area (opened since, or this
    /// one opened again), or for this one before the world was read again (after a save), which lists its things anew;
    /// whatever objects have those indices now would be hit. Null when they fit (or none was given yet: each is checked
    /// on its own).
    /// </summary>
    private string? StaleIds(WorldScene scene) => _ids is { } ids && (ids.Scene != scene || ids.Reads != scene.Reads)
        ? $"The ids are from {(ids.Scene != scene ? "another area" : "before the world was read again")}: list_objects (or get_selection) again for this one."
        : null;

    /// <summary>An object after a change: its new id and where it is now.</summary>
    private object Moved(WorldScene scene, int was, int id)
    {
        var t = scene.Things[id];
        return new
        {
            was,
            id,
            name = _w.NameOfPrefab(t.Prefab),
            x = MathF.Round(t.Position.X, 3),
            y = MathF.Round(t.Position.Y, 3),
            z = MathF.Round(t.Position.Z, 3),
            yaw = MathF.Round(t.Rotation.Y, 1),
        };
    }

    /// <summary>Euler angles each brought within 0 to 360 degrees (a turn of 383 is one of 23).</summary>
    internal static Vector3 Wrapped(Vector3 euler)
    {
        static float W(float a) => (a % 360 + 360) % 360;
        return new Vector3(W(euler.X), W(euler.Y), W(euler.Z));
    }

    /// <summary>A point turned round the vertical by degrees, as the game's yaw turns (90: north becomes east).</summary>
    internal static (float X, float Z) Turned(float x, float z, float degrees)
    {
        float a = degrees * MathF.PI / 180f, c = MathF.Cos(a), s = MathF.Sin(a);
        return (x * c + z * s, -x * s + z * c);
    }

    /// <summary>
    /// move_objects: shifts and turns objects together, as one rigid group (a building keeps its shape), optionally
    /// keeping each the same height above the ground.
    /// </summary>
    [McpServerTool(Name = "move_objects", Title = "Move and turn objects")]
    [Description("Moves objects of the open area (by id) together, as the Select tool does: shifted by dx, dy, dz metres and turned by turn degrees round the vertical (as yaw turns: 90 = a quarter turn clockwise seen from above), all round one point (default: their middle), so that a building moved whole keeps its shape. ground: true keeps each one as high above the ground as it was (the ground where it ends up). A part of a group a mod links into one (a ship and the pieces built on it) brings its whole group. Each object keeps what it holds (chest contents, builder…) but gets a new id: the answer gives them. Refused when one would leave the area. One pending step.")]
    public Task<string> MoveObjects(
        [Description("Their ids (list_objects, get_selection).")] int[] ids,
        [Description("Metres east (negative: west).")] float dx = 0,
        [Description("Metres up (negative: down).")] float dy = 0,
        [Description("Metres north (negative: south).")] float dz = 0,
        [Description("Degrees to turn round the vertical (as yaw: 90 turns north to east).")] float turn = 0,
        [Description("World x of the point to turn round (optional, with pivotZ: their middle).")] float? pivotX = null,
        [Description("World z of that point.")] float? pivotZ = null,
        [Description("Keep each the same height above the ground (default false: dy alone changes heights).")] bool ground = false,
        [Description("A few words for the history.")] string label = "Claude: moved") => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var s = _w.Session!;
        var scene = s.Scene;
        int reads = scene.Reads;
        if (StaleIds(scene) is string stale)
        {
            return stale;
        }
        if (LoneCoordinate(pivotX, pivotZ, "pivotX", "pivotZ") is string lone)
        {
            return lone;
        }
        var (usable, problems) = Usable(scene, ids);
        if (usable.Count == 0)
        {
            return "Nothing moved: " + string.Join("; ", problems);
        }
        // A linked group's part never moves alone (ObjectLinks: a ship): the whole group does, as one block.
        var (ok, _, groups, _) = s.WithLinked(usable);
        if (dx == 0 && dy == 0 && dz == 0 && turn % 360 == 0 && !ground)
        {
            return "Nothing to do: give dx, dy, dz, turn or ground.";
        }
        float px = pivotX ?? ok.Average(i => scene.Things[i].Position.X), pz = pivotZ ?? ok.Average(i => scene.Things[i].Position.Z);
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        var moves = new List<(int Index, Vector3 Position, Vector3 Rotation, float Scale)>();
        foreach (int i in ok)
        {
            var t = scene.Things[i];
            var (ox, oz) = Turned(t.Position.X - px, t.Position.Z - pz, turn);
            float x = px + ox + dx, z = pz + oz + dz;
            if (x < minX || x > maxX || z < minZ || z > maxZ)
            {
                return $"Nothing moved: {i} ({_w.NameOfPrefab(t.Prefab)}) would go to ({x:0.##}, {z:0.##}), outside the area (x {minX} to {maxX}, z {minZ} to {maxZ}).";
            }
            float y = t.Position.Y + dy + (ground ? GroundAt(scene, x, z) - GroundAt(scene, t.Position.X, t.Position.Z) : 0);
            moves.Add((i, new Vector3(x, y, z), Wrapped(t.Rotation + new Vector3(0, turn, 0)), t.Scale));
        }
        var copies = s.Transform(moves, label.StartsWith("Claude", StringComparison.Ordinal) ? label : ClaudesStep(label));
        _w.AfterClaudeEdit(Said.Of("claude.msg.moved", copies.Count));
        GaveIds(scene, reads);
        return ToJson(new { moved = copies.Take(moves.Count).Select((c, k) => Moved(scene, moves[k].Index, c)), wholeGroups = groups, problems, pending = Pending() });
    });

    /// <summary>A place, turn or size for one object (set_transform); what is left out stays as it is.</summary>
    public sealed class TransformSpec
    {
        /// <summary>The object's id.</summary>
        [Description("The object's id (list_objects).")]
        public int Id { get; set; }

        /// <summary>World x (east), in metres; null: as it is.</summary>
        [Description("World x (east), metres (optional: as it is).")]
        public float? X { get; set; }

        /// <summary>The height of its origin; null: as it is.</summary>
        [Description("Height of its origin, metres (optional: as it is).")]
        public float? Y { get; set; }

        /// <summary>World z (north), in metres; null: as it is.</summary>
        [Description("World z (north), metres (optional: as it is).")]
        public float? Z { get; set; }

        /// <summary>Turn round the vertical, in degrees; null: as it is.</summary>
        [Description("Turn round the vertical, degrees (0: its front faces north; 90: east; optional: as it is).")]
        public float? Yaw { get; set; }

        /// <summary>Tilt forward, in degrees; null: as it is.</summary>
        [Description("Tilt forward, degrees (optional: as it is).")]
        public float? Pitch { get; set; }

        /// <summary>Tilt sideways, in degrees; null: as it is.</summary>
        [Description("Tilt sideways, degrees (optional: as it is).")]
        public float? Roll { get; set; }

        /// <summary>Its size, 1 the prefab's own; null: as it is.</summary>
        [Description("Its size: 1 is the prefab's own, 2 twice as large (0.05 to 20; optional: as it is). The game draws and collides at that size; pieces' snap points and support do not grow with it.")]
        public float? Scale { get; set; }

        /// <summary>Stand on the ground at its (new) place, its lowest point touching it, when no y is given.</summary>
        [Description("With no y: stand it on the ground where it ends up, its lowest point touching it (default false).")]
        public bool OnGround { get; set; }
    }

    /// <summary>set_transform: exact places, turns and sizes, each object its own (see <see cref="TransformSpec"/>).</summary>
    [McpServerTool(Name = "set_transform", Title = "Place, turn or size objects exactly")]
    [Description("Puts objects of the open area (by id) exactly where given: position, yaw, pitch, roll and size, each object its own; what is left out stays as it is. A part of a group a mod links into one (a ship and the pieces built on it) takes its whole group along (its root first when both are given). Each keeps what it holds but gets a new id (the answer gives them). Objects that would leave the area are left out and named. One pending step.")]
    public Task<string> SetTransform(
        [Description("The objects and their new values.")] TransformSpec[] objects,
        [Description("A few words for the history.")] string label = "Claude: placed exactly") => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var s = _w.Session!;
        var scene = s.Scene;
        int reads = scene.Reads;
        if (StaleIds(scene) is string stale)
        {
            return stale;
        }
        var (ok, problems) = Usable(scene, objects.Select(o => o.Id));
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        var moves = new List<(int Index, Vector3 Position, Vector3 Rotation, float Scale)>();
        foreach (var o in objects.Where(o => ok.Contains(o.Id)).DistinctBy(o => o.Id))
        {
            var t = scene.Things[o.Id];
            float x = o.X ?? t.Position.X, z = o.Z ?? t.Position.Z;
            if (x < minX || x > maxX || z < minZ || z > maxZ)
            {
                problems.Add($"{o.Id}: ({x:0.##}, {z:0.##}) is outside the area (x {minX} to {maxX}, z {minZ} to {maxZ})");
                continue;
            }
            if (o.Scale is float sc && (!float.IsFinite(sc) || sc < MinScale || sc > MaxScale))
            {
                problems.Add($"{o.Id}: a scale of {sc} is not between {MinScale} and {MaxScale}");
                continue;
            }
            var rot = Wrapped(new Vector3(o.Pitch ?? t.Rotation.X, o.Yaw ?? t.Rotation.Y, o.Roll ?? t.Rotation.Z));
            float scale = o.Scale ?? t.Scale;
            // A scale of 0 is the prefab's own size.
            float y = o.Y ?? (o.OnGround && _w.NameOfPrefab(t.Prefab) is string n
                ? GroundAt(scene, x, z) - Editing.Hammer.Bottom(n, TerrainEditor.App.BlueprintFormats.FromEuler(rot), scale > 0 ? scale : 1)
                : t.Position.Y);
            moves.Add((o.Id, new Vector3(x, y, z), rot, scale));
        }
        if (moves.Count == 0)
        {
            return "Nothing changed: " + string.Join("; ", problems);
        }
        var copies = s.Transform(moves, label.StartsWith("Claude", StringComparison.Ordinal) ? label : ClaudesStep(label));
        _w.AfterClaudeEdit(Said.Of("claude.msg.placed", copies.Count));
        // Then come the other parts of the linked groups moved (ObjectLinks), which went along.
        GaveIds(scene, reads);
        return ToJson(new { placed = copies.Take(moves.Count).Select((c, k) => Moved(scene, moves[k].Index, c)), alongWithGroups = copies.Count - moves.Count, problems, pending = Pending() });
    });

    /// <summary>replace_objects: objects become another kind, where they are and turned as they are (as the Area tool's Replace).</summary>
    [McpServerTool(Name = "replace_objects", Title = "Replace objects with another kind")]
    [Description("Replaces objects of the open area (by id) with objects of another kind (a prefab name from find_prefabs), where they are and turned as they are, as the Area tool's Replace does: a birch for a beech, a stone wall for a wooden one, an iron-banded chest for a wooden one. What the old ones held (chest contents, text…) does not go to the new ones; each keeps its size. The root of a group a mod links into one (a ship) takes its parts away with it (partsTakenAway); a part is replaced alone. The answer gives the new ids. One pending step.")]
    public Task<string> ReplaceObjects(
        [Description("Their ids (list_objects).")] int[] ids,
        [Description("The new kind's prefab name, e.g. Birch1, stone_wall_2x1, piece_chest.")] string prefab,
        [Description("Stand each on the ground where it is, its lowest point touching it (default false: same origin height).")] bool onGround = false) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        int hash = Save.StableHash.Of(prefab);
        bool piece = Terrain.PieceCatalog.Get(hash)?.Tool != null;
        if (Terrain.PieceCatalog.Get(hash) == null && Terrain.PrefabCatalog.Get(hash) == null)
        {
            return $"{prefab} is not an object the game has (find_prefabs).";
        }
        var s = _w.Session!;
        var scene = s.Scene;
        int reads = scene.Reads;
        if (StaleIds(scene) is string stale)
        {
            return stale;
        }
        var (ok, problems) = Usable(scene, ids);
        if (ok.Count == 0)
        {
            return "Nothing replaced: " + string.Join("; ", problems);
        }
        // Each keeps its size; a linked group's root takes its parts away with it (ObjectLinks: a ship), as the Area
        // tool's Replace does: they would have nothing to hang on. A part goes alone.
        var adds = ok.Select(i => scene.Things[i]).Select(t =>
        {
            float y = onGround ? GroundAt(scene, t.Position.X, t.Position.Z) - Editing.Hammer.Bottom(prefab, TerrainEditor.App.BlueprintFormats.FromEuler(t.Rotation), t.Scale > 0 ? t.Scale : 1) : t.Position.Y;
            return (new Editing.NewObject(0, hash, t.Position with { Y = y }, t.Rotation, t.Scale), piece);
        }).ToList();
        var (removed, linked, groups, _) = s.WithLinked(ok, anyPart: false);
        int parts = linked.Count - groups;
        var copies = s.Commit(ClaudesStep(Lang.T("claude.undo.replaced", ok.Count, prefab)), null, removed, adds);
        _w.AfterClaudeEdit(Said.Of("claude.msg.replaced", ok.Count, prefab));
        GaveIds(scene, reads);
        return ToJson(new { replaced = copies.Select((c, k) => Moved(scene, ok[k], c)), partsTakenAway = parts, problems, pending = Pending() });
    });

    /// <summary>
    /// The values an object holds in the save, as (section, key hash, value as text): from its saved bytes in a world,
    /// from its values on the Workshop's plot (which has no world files behind it); null when it cannot be read.
    /// </summary>
    private List<(string Section, int Key, string Value)>? Values(EditSession s, WorldScene.Thing t)
    {
        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        if (_w.InWorkshop || s.Scene.World is not { } world)
        {
            return [.. (s.Edits.FindAdded(t.Id)?.Data ?? []).Where(f => f.Value != null).Select(f => (f.Section, f.Key, f.Value!))];
        }
        byte[]? bytes;
        try
        {
            bytes = TerrainEditor.App.ObjectData.Bytes(world, s.Edits, t.Id);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            bytes = null;
        }
        if (bytes == null)
        {
            return null;
        }
        var z = Save.ZdoData.Parse(bytes);
        return
        [
            .. z.FloatList.Select(v => ("floats", v.Key, F(v.Value))),
            .. z.Vec3List.Select(v => ("vec3", v.Key, $"{F(v.Value.X)} {F(v.Value.Y)} {F(v.Value.Z)}")),
            .. z.QuatList.Select(v => ("quats", v.Key, $"{F(v.Value.X)} {F(v.Value.Y)} {F(v.Value.Z)} {F(v.Value.W)}")),
            .. z.IntList.Select(v => ("ints", v.Key, v.Value.ToString(CultureInfo.InvariantCulture))),
            .. z.LongList.Select(v => ("longs", v.Key, v.Value.ToString(CultureInfo.InvariantCulture))),
            .. z.StringList.Select(v => ("strings", v.Key, v.Value)),
            .. z.ByteList.Select(v => ("bytes", v.Key, Convert.ToBase64String(v.Value))),
        ];
    }

    /// <summary>get_object: everything an object is and holds, as the inspector shows it.</summary>
    [McpServerTool(Name = "get_object", ReadOnly = true, Title = "Everything about an object")]
    [Description("Everything an object of the open area is and holds, as the editor's inspector shows it: name, kind, position, turn (yaw, pitch, roll), size, whether a player built it, a container's contents (item, stack, quality, slot) and every value it keeps in the save by name (a sign's text, a portal's tag, health, fuel, builder (creator, creatorName), owner, a ward's permitted players, a creature's level and tamed state, timers…). Change values with set_object_data, contents with set_contents.")]
    public Task<string> GetObject([Description("The object's id (list_objects).")] int id) => OnUi(() =>
    {
        if (_w.Session is not { } s)
        {
            return "No area is open.";
        }
        int reads = s.Scene.Reads;
        if (StaleIds(s.Scene) is string stale)
        {
            return stale;
        }
        var things = s.Scene.Things;
        if (id < 0 || id >= things.Count || things[id].Gone)
        {
            return "No object with that id in the area.";
        }
        GaveIds(s.Scene, reads);
        var t = things[id];
        var head = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = _w.NameOfPrefab(t.Prefab),
            ["displayName"] = Terrain.PrefabCatalog.DisplayName(t.Prefab),
            ["kind"] = KindOf(t),
            ["x"] = MathF.Round(t.Position.X, 3),
            ["y"] = MathF.Round(t.Position.Y, 3),
            ["z"] = MathF.Round(t.Position.Z, 3),
            ["yaw"] = MathF.Round(t.Rotation.Y, 2),
            ["pitch"] = MathF.Round(t.Rotation.X, 2),
            ["roll"] = MathF.Round(t.Rotation.Z, 2),
            ["scale"] = t.Scale > 0 ? MathF.Round(t.Scale, 3) : 1,
            ["built"] = t.Piece,
        };
        if (t.Runestone)
        {
            head["note"] = "A runestone holds no values of its own: the game builds it from its location.";
            return ToJson(head);
        }
        if (Values(s, t) is not { } values)
        {
            head["note"] = "Its values cannot be read.";
            return ToJson(head);
        }
        var data = new List<object>();
        foreach (var (section, key, value) in values)
        {
            string? name = Terrain.ZdoKeys.NameOf(key);
            if (section == "bytes")
            {
                // Contents are given as items below; other byte values only by size.
                if (key != TerrainEditor.App.ObjectData.ItemsKey)
                {
                    data.Add(new { section, key = name ?? key.ToString(CultureInfo.InvariantCulture), bytes = Convert.FromBase64String(value).Length });
                }
                continue;
            }
            string? note = section == "ints" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) ? Terrain.PrefabCatalog.NameOf(h) : null;
            data.Add(new { section, key = name ?? key.ToString(CultureInfo.InvariantCulture), value = value.Length > LongestText ? value[..LongestText] + "…" : value, note });
        }
        head["values"] = data;
        if (Terrain.PrefabCatalog.Details(t.Prefab) is { ContainerW: > 0 } box)
        {
            head["slots"] = new { columns = box.ContainerW, rows = box.ContainerH };
        }
        if (values.FirstOrDefault(v => v.Section == "bytes" && v.Key == TerrainEditor.App.ObjectData.ItemsKey).Value is string items)
        {
            try
            {
                head["contents"] = Save.InventoryData.Read(Convert.FromBase64String(items)).Items.Select(i => new
                {
                    item = Terrain.PrefabCatalog.NameOf(i.Prefab) ?? i.Prefab.ToString(CultureInfo.InvariantCulture),
                    stack = i.Stack,
                    quality = i.Quality,
                    variant = i.Variant,
                    slot = new[] { i.X, i.Y },
                });
            }
            catch (Exception ex) when (Save.CheckedReads.Damaged(ex))
            {
                head["contents"] = "cannot be read";
            }
        }
        return ToJson(head);
    });

    /// <summary>A value of an object's data to set or take away (set_object_data).</summary>
    public sealed class FieldSpec
    {
        /// <summary>Which list it is in.</summary>
        [Description("Its kind: strings (text), ints (whole numbers), floats, longs (large whole numbers: player ids), vec3 (\"x y z\") or quats (\"x y z w\"). get_object gives each value's.")]
        public string Section { get; set; } = "strings";

        /// <summary>Its name (or hash in decimal).</summary>
        [Description("Its name, e.g. tag, text, health, fuel, creator, creatorName, owner, ownerName, permitted, enabled, level, tamed (or its hash, as get_object gives unknown ones).")]
        public string Key { get; set; } = "";

        /// <summary>The value as text; null takes it away.</summary>
        [Description("The value as text (numbers separated by spaces for vec3 and quats); null or left out: take the value away.")]
        public string? Value { get; set; }
    }

    /// <summary>The sections set_object_data takes (byte arrays only through set_contents).</summary>
    private static readonly HashSet<string> Sections = ["strings", "ints", "floats", "longs", "vec3", "quats"];

    /// <summary>set_object_data: values of an object's data set or taken away, as the inspector does.</summary>
    [McpServerTool(Name = "set_object_data", Title = "Change an object's values")]
    [Description("Sets or takes away values an object keeps in the save, as the editor's inspector does (get_object lists them): a portal's tag (strings tag; two portals with the same tag are linked), a sign's text (strings text), a builder (longs creator: a player id, 0 or none for a ruin; strings creatorName), a ward's owner and permitted players, health, fuel, a creature's level (ints level: stars + 1) or tamed (ints tamed 1), enabled… The object keeps everything else, gets a new id (the answer gives it), one pending step. Change values only when you know what they do: the game may reset or ignore wrong ones.")]
    public Task<string> SetObjectData(
        [Description("The object's id (list_objects).")] int id,
        [Description("The values to set or take away.")] FieldSpec[] values) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var scene = _w.Session!.Scene;
        if (StaleIds(scene) is string stale)
        {
            return stale;
        }
        var (ok, problems) = Usable(scene, [id]);
        if (ok.Count == 0)
        {
            return problems[0];
        }
        if (values.Length == 0)
        {
            return "Nothing to change: give values.";
        }
        foreach (var v in values)
        {
            if (!Sections.Contains(v.Section))
            {
                return $"{v.Key}: the kind is strings, ints, floats, longs, vec3 or quats, not {v.Section} (contents: set_contents).";
            }
            if (string.IsNullOrWhiteSpace(v.Key))
            {
                return "Every value needs a key (its name).";
            }
            if (!Save.ZdoData.Valid(v.Section, v.Value))
            {
                return $"{v.Key}: \"{v.Value}\" is not a value of kind {v.Section}.";
            }
        }
        string name = _w.NameOfPrefab(scene.Things[id].Prefab) ?? "object";
        var (copy, problem) = Rewrite(id, [.. values.Select(v => new TerrainEditor.App.FieldChange(v.Section, v.Key, v.Value))], null, ClaudesStep(Lang.T("claude.undo.edited", name)));
        if (copy is not int newId)
        {
            return problem!;
        }
        _w.AfterClaudeEdit(Said.Of("claude.msg.edited", name));
        return $"Changed {name}; its id is now {newId}. Now pending: {Pending()}";
    });

    /// <summary>
    /// Replaces an object by a copy with these values set (null takes one away) and these contents (null: as they
    /// are), as one pending step: from its saved bytes in a world, from its values on the Workshop's plot.
    /// </summary>
    /// <param name="id">The object's index into the scene's things.</param>
    /// <param name="set">Values to set or remove.</param>
    /// <param name="inv">The container's new contents, or null.</param>
    /// <param name="label">The undo step's name.</param>
    /// <param name="notes">Told of stacks cut to the item's maximum, when given.</param>
    /// <returns>The copy's id, or why nothing changed.</returns>
    internal (int? Id, string? Problem) Rewrite(int id, List<TerrainEditor.App.FieldChange> set, List<TerrainEditor.App.ItemUpload>? inv, string label, ICollection<string>? notes = null)
    {
        var s = _w.Session!;
        int reads = s.Scene.Reads;
        var t = s.Scene.Things[id];
        try
        {
            Editing.NewObject replacement;
            // (The Workshop's plot has no world files behind it: its objects carry their values.)
            if (!_w.InWorkshop && s.Scene.World is { } world && TerrainEditor.App.ObjectData.Bytes(world, s.Edits, t.Id) is { } bytes)
            {
                // A world's object: its own data, changed (as the inspector does).
                var z = TerrainEditor.App.ObjectData.Edited(bytes, set, inv, notes);
                replacement = new(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize());
            }
            else
            {
                // On the Workshop's plot: its values, with these.
                var fields = (s.Edits.FindAdded(t.Id)?.Data ?? []).ToList();
                void Put(string section, int key, string? value)
                {
                    fields.RemoveAll(f => f.Key == key);
                    if (value != null)
                    {
                        fields.Add(new(section, key, value));
                    }
                }
                foreach (var f in set)
                {
                    Put(f.Section, Terrain.ZdoKeys.Parse(f.Key), f.Value);
                }
                if (inv != null)
                {
                    Put("bytes", TerrainEditor.App.ObjectData.ItemsKey, Convert.ToBase64String(TerrainEditor.App.ObjectData.BuildInventory(inv, notes).Write()));
                    Put("ints", TerrainEditor.App.ObjectData.AddedDefaultItemsKey, "1");
                }
                replacement = new(0, t.Prefab, t.Position, t.Rotation, t.Scale, Data: fields);
            }
            var copies = s.Commit(label, null, [id], [(replacement, t.Piece)]);
            GaveIds(s.Scene, reads);
            return (copies[0], null);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            return (null, $"Could not change it: {ex.Message}");
        }
    }
}
