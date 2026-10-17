using System.Numerics;
using System.Text.RegularExpressions;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Dungeon tool: the rooms of a dungeon of the open area (Frost Cave, crypt...), edited the game's way. A room is
/// added at a free opening of another (turned and moved so the two openings meet, see Dungeons.Attach), selected,
/// deleted, and the open ends closed with end caps. Each change rewrites the dungeon object's room list as one step of
/// the history (Commit), so undo, saving and Apply live work as for any other object. Everything here is in Unity's
/// world space; the view hands in rays.
/// </summary>
public sealed partial class DungeonTool
{
    /// <summary>
    /// The dungeon being edited: its object's position (an edit makes it a new object of the scene, at the same place).
    /// </summary>
    public Vector3? Current { get; private set; }

    /// <summary>
    /// The room the next click adds (a name of the catalogue); null when none is chosen.
    /// </summary>
    public string? Chosen { get; private set; }

    /// <summary>
    /// Which of the chosen room's openings that fit the hovered one joins it (counted by Turn, taken modulo how many
    /// fit).
    /// </summary>
    public int Join { get; private set; }

    /// <summary>
    /// Rooms added with what the game makes in them (chests, creatures' spawners, torches, ice): rolled from the room's
    /// place the way the game rolls them (Dungeons.Contents).
    /// </summary>
    public bool WithContents { get; set; } = true;

    /// <summary>
    /// The room clicked last (an index into the current dungeon's list), found again by its prefab and place each time,
    /// so an undo or redo that shifts or takes away rooms never leaves it on another room; null when it is gone.
    /// </summary>
    public int? Selected
    {
        get
        {
            if (_selected is not { } s || Dungeon is not { } d)
            {
                return null;
            }
            bool Same(Dungeons.Placed p) => p.Hash == s.Hash && Vector3.DistanceSquared(p.Position, s.Position) < 1e-6f;
            if (s.Index < d.Rooms.Count && Same(d.Rooms[s.Index]))
            {
                return s.Index;
            }
            int at = d.Rooms.FindIndex(Same);
            return at >= 0 ? at : null;
        }
        private set => _selected = value is int i && Dungeon is { } d && i < d.Rooms.Count ? (i, d.Rooms[i].Hash, d.Rooms[i].Position) : null;
    }

    /// <summary>
    /// The selected room: its index then, its prefab's hash and its place (Selected finds it again by them).
    /// </summary>
    private (int Index, int Hash, Vector3 Position)? _selected;

    /// <summary>
    /// The free opening the pointer is over (its index in FreeEnds); null when none.
    /// </summary>
    public int? HoverEnd { get; private set; }

    /// <summary>
    /// The room the pointer is over (an index into the current dungeon's list); null when it is over an opening, a
    /// joint or nothing.
    /// </summary>
    public int? HoverRoom { get; private set; }

    /// <summary>
    /// A joint that can take a door (its index in DoorJoints), pointed at.
    /// </summary>
    public int? HoverJoint { get; private set; }

    /// <summary>
    /// A door where an added room joins, by the game's chance (Dungeons.RollDoor).
    /// </summary>
    public bool WithDoors { get; set; } = true;

    /// <summary>
    /// Numbers from 0 to 1 for the door rolls (tests set their own).
    /// </summary>
    internal Func<float> Random { get; set; } = System.Random.Shared.NextSingle;

    /// <summary>
    /// Where two rooms of the open dungeon meet and a door can stand.
    /// </summary>
    public List<Dungeons.Joint> DoorJoints => Derive()?.Joints ?? [];

    /// <summary>
    /// The scene's doors (of the dungeon's door kinds) standing at a joint.
    /// </summary>
    /// <returns>Indices into the scene's things; empty when there is none or no dungeon is open.</returns>
    public List<int> DoorsAt(Dungeons.Joint j) => Derive() is { } c && c.Doors.TryGetValue(Spot(j.Position), out var at) ? [.. at] : [];

    /// <summary>
    /// How many pairs of the open dungeon's rooms overlap.
    /// </summary>
    public int OverlappingPairs => Derive()?.Overlapping ?? 0;

    /// <summary>
    /// What the drawing, the pointer and the panel ask of the open dungeon (its open ends, door joints, doors,
    /// overlaps), worked out once for each change of it, not for each frame or pointer move.
    /// </summary>
    /// <param name="D">The dungeon it was worked out for.</param>
    /// <param name="Version">The edit store's version then.</param>
    /// <param name="Generation">The session's generation then (it moves when the live game changes things in place).</param>
    /// <param name="Count">How many things the scene had then.</param>
    /// <param name="Free">The free openings.</param>
    /// <param name="Joints">The joints that can take one of the dungeon's doors.</param>
    /// <param name="Doors">The scene's doors of the dungeon's kinds, by their place rounded to 5 cm (see Spot).</param>
    /// <param name="Overlapping">How many pairs of rooms overlap.</param>
    private sealed record Derived(DungeonRooms.Dungeon D, int Version, int Generation, int Count, List<Dungeons.OpenEnd> Free, List<Dungeons.Joint> Joints,
        Dictionary<(long, long, long), List<int>> Doors, int Overlapping);

    /// <summary>
    /// The last Derived, kept while the dungeon, the edit version, the session's generation and the thing count stay the
    /// same.
    /// </summary>
    private Derived? _derived;

    /// <summary>
    /// A position rounded to 5 cm, as a dictionary key for doors standing at a joint.
    /// </summary>
    private static (long, long, long) Spot(Vector3 p) => ((long)MathF.Round(p.X * 20), (long)MathF.Round(p.Y * 20), (long)MathF.Round(p.Z * 20));

    /// <summary>
    /// The Derived of the open dungeon, worked out again only when it, the edits, the session's generation or the
    /// scene's thing count changed; null when no scene or dungeon is open.
    /// </summary>
    private Derived? Derive()
    {
        if (Scene is not { } s || Dungeon is not { } d)
        {
            return null;
        }
        int version = s.Session?.Edits.Version ?? 0, generation = s.Session?.Generation ?? 0, count = s.Things.Count;
        if (_derived is { } c && ReferenceEquals(c.D, d) && c.Version == version && c.Generation == generation && c.Count == count)
        {
            return c;
        }
        var kinds = d.Kind.Doors.Select(x => StableHash.Of(x.Prefab)).ToHashSet();
        var doors = new Dictionary<(long, long, long), List<int>>();
        lock (s.Things)
        {
            for (int i = 0; i < s.Things.Count; i++)
            {
                var t = s.Things[i];
                if (!t.Gone && kinds.Contains(t.Prefab))
                {
                    (doors.TryGetValue(Spot(t.Position), out var l) ? l : doors[Spot(t.Position)] = []).Add(i);
                }
            }
        }
        int overlapping = d.Rooms.Select((r, i) => Dungeons.Overlaps(d.Rooms, r, i).Count > 0 ? 1 : 0).Sum() / 2;
        var joints = Dungeons.Joints(d.Rooms).Where(j => j.DoorAllowed && Dungeons.DoorsFor(d.Kind, j.Type).Any()).ToList();
        return _derived = new Derived(d, version, generation, count, Dungeons.Openings(d.Rooms, freeOnly: true), joints, doors, overlapping);
    }

    /// <summary>
    /// Raised when what the tool shows changed (the open dungeon, the choice, the hover, the selection, or an edit):
    /// the view redraws and the panel updates.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// A line for the status bar: what a change did, or why it did nothing (in the user's language, and in English for
    /// Claude).
    /// </summary>
    public event Action<Said>? Message;

    /// <summary>
    /// The scene: set by the view when it opens an area.
    /// </summary>
    public WorldScene? Scene { get; set; }

    /// <summary>
    /// The dungeons of the open area (empty without a scene).
    /// </summary>
    public IReadOnlyList<DungeonRooms.Dungeon> All => Scene == null ? [] : DungeonRooms.Of(Scene);

    /// <summary>
    /// The open dungeon: the one whose object stands at Current (within 10 cm); null when none is open or it is not
    /// found.
    /// </summary>
    public DungeonRooms.Dungeon? Dungeon => Current is { } at ? All.FirstOrDefault(d => Vector3.DistanceSquared(d.Thing.Position, at) < 0.01f) : null;

    /// <summary>
    /// The open dungeon's openings no room joins yet.
    /// </summary>
    public List<Dungeons.OpenEnd> FreeEnds => Derive()?.Free ?? [];

    // ---- Names shown for the kinds of dungeon.
    /// <summary>
    /// The name shown for a kind of dungeon, in the language in use and in English (Claude's): the game's own for the
    /// known ones, else its prefab name without DG_ and split into words (the same in both).
    /// </summary>
    public static Said KindName(Dungeons.Kind k) => k.Name switch
    {
        "DG_Cave" => Said.Of("dungeon.kind.frostCave"),
        "DG_SunkenCrypt" => Said.Of("dungeon.kind.sunkenCrypt"),
        "DG_ForestCrypt" => Said.Of("dungeon.kind.burialChambers"),
        "DG_HalfBurried_ForestCrypt" => Said.Of("dungeon.kind.burialChambersHalf"),
        "DG_DvergrTown" => Said.Of("dungeon.kind.infestedMine"),
        "DG_DvergrBoss" => Said.Of("dungeon.kind.infestedCitadel"),
        "DG_Hildir_Cave" => Said.Of("dungeon.kind.howlingCavern"),
        "DG_Hildir_ForestCrypt" => Said.Of("dungeon.kind.smoulderingTomb"),
        "DG_Hildir_PlainsFortress" => Said.Of("dungeon.kind.sealedTower"),
        "DG_GoblinCamp" => Said.Of("dungeon.kind.fulingVillage"),
        "DG_MeadowsVillage" => Said.Of("dungeon.kind.meadowsVillage"),
        "DG_MeadowsFarm" => Said.Of("dungeon.kind.meadowsFarm"),
        _ => Said.Raw(Words().Replace(k.Name.Replace("DG_", "", StringComparison.Ordinal).Replace('_', ' '), " $1").Trim()),
    };

    /// <summary>
    /// A capital letter after a small one: where KindName puts a space.
    /// </summary>
    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex Words();

    /// <summary>
    /// One line for a dungeon in a list: its kind, how many rooms, and where it is (x, z in metres).
    /// </summary>
    public static string Describe(DungeonRooms.Dungeon d) =>
        Lang.T("dungeon.label.entry", KindName(d.Kind).Shown, d.Rooms.Count, MathF.Round(d.Thing.Position.X) + 0f, MathF.Round(d.Thing.Position.Z) + 0f);

    // ---- Choosing.
    /// <summary>
    /// Opens a dungeon for editing (null closes it); the selection and the hovered opening and room are cleared.
    /// </summary>
    public void Open(DungeonRooms.Dungeon? d)
    {
        Current = d?.Thing.Position;
        Selected = null;
        HoverEnd = HoverRoom = null;
        Changed?.Invoke();
    }

    /// <summary>
    /// Chooses the room the next click adds (null: none), starting again at its first fitting opening.
    /// </summary>
    public void Choose(string? room)
    {
        Chosen = room;
        Join = 0;
        Changed?.Invoke();
    }

    /// <summary>
    /// The rooms the open dungeon can take, the game's way (its themes), by name.
    /// </summary>
    public IEnumerable<Dungeons.Room> Palette => Dungeon is { } d ? Dungeons.RoomsFor(d.Kind) : [];

    // ---- The pointer (a ray in world space).
    /// <summary>
    /// Works out what the pointer's ray is over: a free opening first, else a joint that can take a door, else a room.
    /// Raises Changed when that changed.
    /// </summary>
    /// <param name="origin">Where the ray starts, in world space.</param>
    /// <param name="dir">Its direction (of length 1: distances along it are metres).</param>
    public void Hover(Vector3 origin, Vector3 dir)
    {
        int? end = null, room = null, joint = null;
        if (Dungeon is { } d)
        {
            // A free opening near the ray (within 1.5 m of it), the nearest along it; else a joint that
            // can take a door; else a room.
            end = Nearest([.. FreeEnds.Select(e => e.Position)], origin, dir);
            if (end == null)
            {
                joint = Nearest([.. DoorJoints.Select(j => j.Position)], origin, dir);
            }
            if (end == null && joint == null)
            {
                room = RoomAt(d.Rooms, origin, dir);
            }
        }
        if (end != HoverEnd || room != HoverRoom || joint != HoverJoint)
        {
            (HoverEnd, HoverRoom, HoverJoint) = (end, room, joint);
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// The point nearest along the ray that it passes within 1.5 m of.
    /// </summary>
    /// <returns>The point's index, or null when the ray passes none within 1.5 m in front of it.</returns>
    private static int? Nearest(List<Vector3> points, Vector3 origin, Vector3 dir)
    {
        int? found = null;
        float best = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            float t = Vector3.Dot(points[i] - origin, dir);
            if (t > 0 && t < best && Vector3.Distance(origin + dir * t, points[i]) < 1.5f)
            {
                best = t;
                found = i;
            }
        }
        return found;
    }

    /// <summary>
    /// The room whose box the ray meets first from outside, or the smallest box it starts in (the camera inside a room
    /// sees the rooms around it through their walls only with a cut).
    /// </summary>
    /// <returns>The room's index, or null when the ray meets none.</returns>
    public static int? RoomAt(IReadOnlyList<Dungeons.Placed> rooms, Vector3 origin, Vector3 dir)
    {
        int? hit = null;
        float best = float.MaxValue;
        for (int i = 0; i < rooms.Count; i++)
        {
            var (c, h, q) = Dungeons.Box(rooms[i]);
            if (h.X == 0 || h.Z == 0)
            {
                h = new Vector3(Math.Max(h.X, 0.5f), Math.Max(h.Y, 0.5f), Math.Max(h.Z, 0.5f));
            }
            var inv = Quaternion.Inverse(q);
            var o = Vector3.Transform(origin - c, inv);
            var d = Vector3.Transform(dir, inv);
            if (Picking.HitBox(o, d, -h, h) is float t)
            {
                // From inside a box (t = 0) the smaller box wins.
                float key = t > 0 ? t : -1f / (h.X * h.Y * h.Z);
                if (key < best)
                {
                    best = key;
                    hit = i;
                }
            }
        }
        return hit;
    }

    /// <summary>
    /// Where the chosen room would go at the hovered opening; null when nothing would be added.
    /// </summary>
    public Dungeons.Placed? Preview => Chosen != null && HoverEnd is int e ? PlaceAt(e, Chosen, Join) : null;

    /// <summary>
    /// Where a room would go at a free opening of the open dungeon, joined by the join-th of its openings that fit
    /// (counting round); null when there is no such opening or room, or none of its openings fits.
    /// </summary>
    /// <param name="end">The free opening, an index into FreeEnds.</param>
    /// <param name="room">The room's name.</param>
    /// <param name="join">Which of its fitting openings joins.</param>
    public Dungeons.Placed? PlaceAt(int end, string room, int join)
    {
        var ends = FreeEnds;
        if (end < 0 || end >= ends.Count || Dungeons.RoomOf(room) is not { } r)
        {
            return null;
        }
        var e = ends[end];
        var matching = Dungeons.Matching(r, e.Type).ToList();
        return matching.Count == 0 ? null : Dungeons.Attach(r, matching[(join % matching.Count + matching.Count) % matching.Count], e.Position, e.Rotation);
    }

    /// <summary>
    /// Why a room placed there would not fit: overlapping rooms, or outside the dungeon's box (the game's own checks
    /// when it generates; a list made here is built anyway, so these are warnings).
    /// </summary>
    /// <param name="p">The placed room.</param>
    /// <param name="skip">A room of the list not to check against (the one being replaced), or -1.</param>
    /// <returns>The warning (for the user, and in English for Claude), or null when it fits.</returns>
    public Said? Problem(Dungeons.Placed p, int skip = -1)
    {
        if (Dungeon is not { } d)
        {
            return null;
        }
        var over = Dungeons.Overlaps(d.Rooms, p, skip);
        if (over.Count > 0)
        {
            return Said.Of("dungeon.error.overlaps", string.Join(", ", over.Select(i => d.Rooms[i].Name).Distinct()));
        }
        return Dungeons.Inside(d.Kind, d.Thing.Position, p) ? null
            : Said.Of("dungeon.error.outside", d.Kind.ZoneSize.X, d.Kind.ZoneSize.Z);
    }

    /// <summary>
    /// R: the next of the chosen room's openings that can join the hovered one.
    /// </summary>
    public void Turn()
    {
        Join++;
        Changed?.Invoke();
    }

    // ---- Changes: each one a step of the history.
    /// <summary>
    /// A click: adds the chosen room at the hovered opening (with its contents and door rolls) as one step of the
    /// history, or adds or removes a door at the hovered joint, or else selects the hovered room.
    /// </summary>
    /// <returns>Whether the click did something (false: it was over nothing).</returns>
    public bool Click()
    {
        if (Preview is { } p && Dungeon != null)
        {
            Add(p);
            return true;
        }
        if (HoverJoint is int ji && Dungeon is { } dj && ji < DoorJoints.Count)
        {
            ToggleDoor(dj, DoorJoints[ji]);
            return true;
        }
        Selected = HoverRoom;
        Changed?.Invoke();
        if (HoverRoom is int r && Dungeon is { } dd)
        {
            Message?.Invoke(Said.Of("dungeon.msg.deleteRemoves", dd.Rooms[r].Name));
        }
        return HoverRoom != null;
    }

    /// <summary>
    /// Removes the selected room as one step of the history, with the objects in it (Inside) and the doors at its
    /// joints.
    /// </summary>
    /// <returns>False when no room is selected.</returns>
    public bool Delete() => Selected is int i && Remove(i);

    /// <summary>
    /// Adds a placed room to the open dungeon as one step of the history, with its contents (WithContents) and the
    /// door the game would roll where it joins (WithDoors); it is selected. Message tells what was done.
    /// </summary>
    /// <param name="p">The room, placed (PlaceAt, Preview).</param>
    /// <param name="label">The history's name for the step; null: "Dungeon: added" and its name.</param>
    /// <returns>False when no dungeon is open.</returns>
    public bool Add(Dungeons.Placed p, string? label = null)
    {
        if (Dungeon is not { } d)
        {
            return false;
        }
        var problem = Problem(p);
        var rooms = d.Rooms.ToList();
        rooms.Add(p);
        var made = ContentsOf(d, [p]);
        int objects = made.Count, doors = 0;
        // Its door, as the game rolls one where a room joins (on the opening it took).
        if (WithDoors)
        {
            foreach (var j in Dungeons.Joints(rooms).Where(j => j.Second == rooms.Count - 1))
            {
                if (Dungeons.RollDoor(d.Kind, j, Random) is string door)
                {
                    doors++;
                    made.Add((new NewObject(0, StableHash.Of(door), j.Position, BlueprintFormats.ToEuler(j.Rotation), 0), false));
                }
            }
        }
        var added = (objects > 0, doors) switch
        {
            (false, 0) => Said.Of("dungeon.msg.added", p.Name),
            (false, 1) => Said.Of("dungeon.msg.addedDoor", p.Name),
            (false, _) => Said.Of("dungeon.msg.addedDoors", p.Name, doors),
            (true, 0) => Said.Of("dungeon.msg.addedObjects", p.Name, objects),
            (true, 1) => Said.Of("dungeon.msg.addedObjectsDoor", p.Name, objects),
            (true, _) => Said.Of("dungeon.msg.addedObjectsDoors", p.Name, objects, doors),
        };
        // Selected before the change is told (Changed): the panel shows Delete for it at once.
        _selected = (rooms.Count - 1, p.Hash, p.Position);
        Commit(d, rooms, label ?? Lang.T("dungeon.undo.added", p.Name), added + (problem ?? Said.Empty), add: made);
        return true;
    }

    /// <summary>
    /// Removes a room of the open dungeon as one step of the history, with the objects in it (Inside) and the doors
    /// at its joints. Message tells what was done.
    /// </summary>
    /// <param name="i">The room, an index into the dungeon's rooms.</param>
    /// <param name="label">The history's name for the step; null: "Dungeon: removed" and its name.</param>
    /// <returns>False when no dungeon is open or there is no such room.</returns>
    public bool Remove(int i, string? label = null)
    {
        if (Dungeon is not { } d || i < 0 || i >= d.Rooms.Count)
        {
            return false;
        }
        var rooms = d.Rooms.ToList();
        var gone = rooms[i];
        string name = gone.Name;
        rooms.RemoveAt(i);
        Selected = null;
        // What the game made in the room (chests, spawners, ice, torches: objects of their own) goes with
        // it, unless another room holds it too.
        var inside = Inside(gone, rooms);
        foreach (var j in Dungeons.Joints(d.Rooms).Where(j => j.First == i || j.Second == i))
        {
            inside.AddRange(DoorsAt(j).Where(k => !inside.Contains(k)));
        }
        var removed = inside.Count > 0 ? Said.Of("dungeon.msg.removedObjects", name, inside.Count) : Said.Of("dungeon.msg.removed", name);
        Commit(d, rooms, label ?? Lang.T("dungeon.undo.removed", name), removed + Said.Of("dungeon.msg.openNow"), inside);
        return true;
    }

    /// <summary>
    /// The scene's objects (not the dungeon itself) the room holds: within its box, and nearer its middle than any
    /// other room's whose box holds them too (rooms meet at their walls; an end cap, flat, gets 1.5 m of depth each
    /// side).
    /// </summary>
    /// <returns>Indices into the scene's things.</returns>
    public List<int> Inside(Dungeons.Placed room, IReadOnlyList<Dungeons.Placed> others)
    {
        var found = new List<int>();
        if (Scene == null)
        {
            return found;
        }
        static float? In(Dungeons.Placed r, Vector3 p)
        {
            var (c, h, q) = Dungeons.Box(r);
            h = Vector3.Max(h, new Vector3(1.5f));
            var l = Vector3.Transform(p - c, Quaternion.Inverse(q));
            return MathF.Abs(l.X) <= h.X + 0.05f && MathF.Abs(l.Y) <= h.Y + 0.05f && MathF.Abs(l.Z) <= h.Z + 0.05f ? Vector3.Distance(p, c) : null;
        }
        lock (Scene.Things)
        {
            for (int i = 0; i < Scene.Things.Count; i++)
            {
                var t = Scene.Things[i];
                if (t.Gone || Dungeons.KindOf(t.Prefab) != null || In(room, t.Position) is not float mine)
                {
                    continue;
                }
                if (!others.Any(o => In(o, t.Position) is float theirs && theirs < mine))
                {
                    found.Add(i);
                }
            }
        }
        return found;
    }

    /// <summary>
    /// Puts an end cap on every free opening as one step of the history: of the open dungeon's rooms, one whose only
    /// opening has that type and is not an entrance (the game's end caps, highest EndCapPrio first, as
    /// DungeonGenerator.PlaceEndCaps prefers).
    /// </summary>
    /// <param name="label">The history's name for the step; null: "Dungeon: closed" and how many.</param>
    /// <returns>How many end caps were added.</returns>
    public int CloseOpenEnds(string? label = null)
    {
        if (Dungeon is not { } d)
        {
            return 0;
        }
        var caps = Dungeons.RoomsFor(d.Kind).Where(r => r.EndCap && r.Openings.Length == 1).OrderByDescending(r => r.EndCapPrio).ThenBy(r => r.Name, StringComparer.Ordinal).ToList();
        var rooms = d.Rooms.ToList();
        int added = 0, left = 0, before = rooms.Count;
        foreach (var end in Dungeons.Openings(d.Rooms, freeOnly: true))
        {
            var cap = caps.FirstOrDefault(c => c.Openings[0].Type == end.Type && !c.Openings[0].Entrance);
            if (cap == null)
            {
                left++;
                continue;
            }
            rooms.Add(Dungeons.Attach(cap, cap.Openings[0], end.Position, end.Rotation));
            added++;
        }
        if (added > 0)
        {
            Commit(d, rooms, label ?? Lang.T("dungeon.undo.closed", added),
                Said.Of("dungeon.msg.closed", added) + (left > 0 ? Said.Of("dungeon.msg.capsLeft", left) : Said.Empty), add: ContentsOf(d, rooms.Skip(before)));
        }
        else
        {
            Message?.Invoke(left > 0 ? Said.Of("dungeon.msg.noCap", left) : Said.Of("dungeon.msg.noOpenEnd"));
        }
        return added;
    }

    /// <summary>
    /// A door at a joint: the one there taken away, or the dungeon's door for that kind of opening put there (facing as
    /// the first room's opening, where the game puts it).
    /// </summary>
    /// <param name="d">The dungeon.</param>
    /// <param name="j">The joint.</param>
    /// <param name="label">The history's name for the step; null: "Dungeon: added a door" or "removed a door".</param>
    public void ToggleDoor(DungeonRooms.Dungeon d, Dungeons.Joint j, string? label = null)
    {
        if (Scene?.Session is not { } s)
        {
            return;
        }
        var there = DoorsAt(j);
        if (there.Count > 0)
        {
            s.Commit(label ?? Lang.T("dungeon.undo.doorRemoved"), null, there, []);
            Message?.Invoke(Said.Of("dungeon.msg.doorRemoved"));
        }
        else if (Dungeons.DoorsFor(d.Kind, j.Type).FirstOrDefault() is { Prefab: { Length: > 0 } door })
        {
            s.Commit(label ?? Lang.T("dungeon.undo.doorAdded"), null, [], [(new NewObject(0, StableHash.Of(door), j.Position, BlueprintFormats.ToEuler(j.Rotation), 0), false)]);
            Message?.Invoke(Said.Of("dungeon.msg.doorAdded", door));
        }
        else
        {
            Message?.Invoke(Said.Of("dungeon.msg.noDoor"));
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// The objects the game makes with these rooms of the dungeon (none when WithContents is off), each with whether it
    /// is a player-built piece.
    /// </summary>
    private List<(NewObject, bool)> ContentsOf(DungeonRooms.Dungeon d, IEnumerable<Dungeons.Placed> rooms)
    {
        var list = new List<(NewObject, bool)>();
        if (!WithContents || Scene == null)
        {
            return list;
        }
        foreach (var r in rooms)
        {
            foreach (var m in Dungeons.Contents(r, d.Kind, d.Thing.Position, Scene.World.Seed))
            {
                int prefab = StableHash.Of(m.Prefab);
                list.Add((new NewObject(0, prefab, m.Position, BlueprintFormats.ToEuler(m.Rotation), 0), Terrain.PieceCatalog.Get(prefab)?.Tool != null));
            }
        }
        return list;
    }

    /// <summary>
    /// Rewrites the dungeon object with this room list as one step of the history: the old object goes and a copy with
    /// the new list comes (with the other objects added), then Message tells what was done.
    /// </summary>
    /// <param name="d">The dungeon changed.</param>
    /// <param name="rooms">Its new room list.</param>
    /// <param name="label">The history's name for the step.</param>
    /// <param name="message">What the status line says it did (for the user, and in English for Claude).</param>
    /// <param name="alsoRemove">Other things to take away (indices into the scene's things).</param>
    /// <param name="add">Other objects to add, each with whether it is a player-built piece.</param>
    private void Commit(DungeonRooms.Dungeon d, List<Dungeons.Placed> rooms, string label, Said message, IReadOnlyCollection<int>? alsoRemove = null,
        IReadOnlyList<(NewObject, bool)>? add = null)
    {
        if (Scene?.Session is not { } s)
        {
            return;
        }
        byte[]? bytes = ObjectData.Bytes(Scene.World, s.Edits, d.Thing.Id);
        if (bytes == null)
        {
            Message?.Invoke(Said.Of("dungeon.msg.unreadable"));
            return;
        }
        var z = ZdoData.Parse(Dungeons.WithRooms(bytes, rooms));
        var adds = new List<(NewObject, bool)> { (new NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize()), false) };
        adds.AddRange(add ?? []);
        s.Commit(label, null, [d.Index, .. alsoRemove ?? []], adds);
        HoverEnd = null;
        Message?.Invoke(message + Said.Of("dungeon.msg.undoSave"));
        Changed?.Invoke();
    }
}
