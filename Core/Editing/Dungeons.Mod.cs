using System.Globalization;
using System.Numerics;
using System.Text.Json;
using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;
using Vector3 = System.Numerics.Vector3;

namespace TerrainEditor.Editing;

// Expand World Data's room and dungeon files laid over the game's catalogue.
public static partial class Dungeons
{
    /// <summary>The room themes by name (case does not matter), with their flag values, as the game's code has them.</summary>
    private static volatile Dictionary<string, int> _themes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The open world's room and dungeon files, null for none.</summary>
    private static volatile DungeonRules? _mod;

    /// <summary>Takes the room themes as GameCatalogues reads them from the game's code (a JSON object of name: value).</summary>
    /// <param name="json">The themes.</param>
    public static void UseThemes(string json)
    {
        using var doc = JsonDocument.Parse(json);
        _themes = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.OrdinalIgnoreCase);
        Refresh();
    }

    /// <summary>
    /// Lays the open world's Expand World Data room and dungeon files over the game's catalogue (null: the game's).
    /// </summary>
    /// <param name="mod">The files, or null.</param>
    public static void UseMod(DungeonRules? mod)
    {
        _mod = mod;
        Refresh();
    }

    /// <summary>A catalogue of a flow of work's own, over the one in use (Scope); null for none.</summary>
    private static readonly AsyncLocal<Catalog?> Scoped = new();

    /// <summary>The catalogue read: the flow of work's own, else the one in use.</summary>
    private static Catalog Current => Scoped.Value ?? _all;

    /// <summary>
    /// The game's catalogue with these files laid over it for the calling flow of work only (and the tasks it starts),
    /// until disposed; the one in use stays as it is for every other (the tests: a catalogue of their own beside others
    /// running).
    /// </summary>
    /// <param name="mod">The files.</param>
    internal static IDisposable Scope(DungeonRules mod)
    {
        var was = Scoped.Value;
        Scoped.Value = Apply(_game, mod, _themes);
        return new Restore(() => Scoped.Value = was);
    }

    /// <summary>Puts back what was there when disposed.</summary>
    /// <param name="undo">What puts it back.</param>
    private sealed class Restore(Action undo) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose() => undo();
    }

    /// <summary>The catalogue in use made again from the game's, the themes and the files.</summary>
    private static void Refresh() => _all = _mod is { } mod ? Apply(_game, mod, _themes) : _game;

    /// <summary>
    /// The game's catalogue with the files laid over it, as the mod changes the game's: the room file's rooms in place
    /// of the game's (a game room the file leaves out is kept for drawing, but no dungeon takes it), a copy
    /// ("name:suffix") a room of its own built as its game room, new theme names given the next flags; the dungeon
    /// file's generators over the game's of the same name.
    /// </summary>
    private static Catalog Apply(Catalog game, DungeonRules mod, Dictionary<string, int> known)
    {
        var themes = new Dictionary<string, int>(known, StringComparer.OrdinalIgnoreCase);
        var rooms = mod.Rooms == null ? new Dictionary<int, Room>(game.Rooms) : RoomsOf(game, mod.Rooms, themes);
        var kinds = new Dictionary<int, Kind>(game.Kinds);
        foreach (var d in mod.Dungeons)
        {
            if (kinds.TryGetValue(StableHash.Of(d.Name), out var kind))
            {
                kinds[kind.Hash] = KindWith(kind, d, themes, game.Rooms.Values);
            }
        }
        return new Catalog(rooms, kinds);
    }

    /// <summary>The room file's rooms over the game's (RoomManager), in the file's order (a later one of a name wins).</summary>
    private static Dictionary<int, Room> RoomsOf(Catalog game, IReadOnlyList<RoomEntry> entries, Dictionary<string, int> themes)
    {
        var rooms = game.Rooms.ToDictionary(kv => kv.Key, kv => kv.Value with { Enabled = false });
        foreach (var e in entries)
        {
            if (game.Rooms.GetValueOrDefault(StableHash.Of(e.Name.Split(':')[0])) is not { } original)
            {
                continue;
            }
            var room = original with
            {
                Name = e.Name,
                Hash = StableHash.Of(e.Name),
                Theme = ThemeOf(e.Theme, themes, create: true),
                Size = VectorXZY(e.Size, Vector3.Zero),
                EndCap = e.EndCap,
                Entrance = e.Entrance,
                Divider = e.Divider,
                EndCapPrio = e.EndCapPriority,
                Weight = e.Weight,
                Perimeter = e.Perimeter,
                MinPlaceOrder = e.MinPlaceOrder,
                Enabled = e.Enabled,
                Openings = OpeningsOf(original.Openings, e.Connections),
                Game = original.Name,
                Added = e.Objects,
                Swaps = e.Swaps,
                Data = e.Data,
            };
            rooms[room.Hash] = room;
        }
        return rooms;
    }

    /// <summary>
    /// A room's openings: its game room's, each the file lists changed (its type, way in, door, place and turn when
    /// given), one past them added at the room's origin.
    /// </summary>
    private static Opening[] OpeningsOf(Opening[] game, IReadOnlyList<RoomConnection> connections)
    {
        var openings = game.ToList();
        for (int i = 0; i < connections.Count; i++)
        {
            var c = connections[i];
            var o = i < openings.Count ? openings[i] : new Opening("", false, false, Vector3.Zero, Quaternion.Identity);
            string[] at = Split(c.Position);
            var changed = o with
            {
                Type = c.Type,
                Entrance = c.Entrance,
                AllowDoor = c.Door == "true",
                OnlyIfOther = c.Door == "other",
                ExitOnly = c.ExitOnly,
                Position = at.Length > 2 ? new Vector3(Number(at, 0), Number(at, 2), Number(at, 1)) : o.Position,
                Rotation = at.Length > 3 ? BlueprintFormats.FromEuler(new Vector3(Number(at, 4), Number(at, 3), Number(at, 5))) : o.Rotation,
            };
            if (i < openings.Count)
            {
                openings[i] = changed;
            }
            else
            {
                openings.Add(changed);
            }
        }
        return [.. openings];
    }

    /// <summary>A generator with the dungeon file's settings (the mod's Override and SetupAvailableRooms).</summary>
    private static Kind KindWith(Kind kind, DungeonEntry d, Dictionary<string, int> themes, IEnumerable<Room> game)
    {
        // Room names as the game's are written (the mod's ParseRooms: case does not matter).
        var names = game.Select(r => r.Name).Distinct().ToDictionary(n => n, StringComparer.OrdinalIgnoreCase);
        string Fix(string name) => names.GetValueOrDefault(name, name);
        return kind with
        {
            Themes = ThemeOf(d.Themes, themes, create: false),
            Interior = !(d.Algorithm.Equals("CampGrid", StringComparison.OrdinalIgnoreCase) || d.Algorithm.Equals("CampRadial", StringComparison.OrdinalIgnoreCase)),
            ZoneSize = d.Bounds == "" ? new Vector3(64f, 256f, 64f)
                : d.Bounds.Contains(',', StringComparison.Ordinal) ? VectorXZY(d.Bounds, new Vector3(64f, 256f, 64f)) : Vector3.One * Number([d.Bounds], 0),
            CustomInterior = d.InteriorTransform,
            BaseSeed = d.AddBaseSeed,
            Doors = [.. d.Doors.Where(x => Terrain.PrefabCatalog.Get(StableHash.Of(x.Prefab)) != null).Select(x => (x.Prefab, x.Type, x.Chance))],
            DoorChance = d.DoorChance,
            Rooms = (d.MinRooms, d.MaxRooms),
            Required = [.. d.Required.Select(Fix)],
            Excluded = d.Excluded.Select(Fix).ToHashSet(StringComparer.Ordinal),
            Limits = [.. d.Limits.Select(l => l with { Room = Fix(l.Room) })],
            Swaps = d.Swaps,
            Data = d.Data,
        };
    }

    /// <summary>
    /// Theme flags from a list of names as the mod reads one (DataManager.ToEnum): each name's value added, a number
    /// as it is; a name not known passed over, or, for a room (<paramref name="create"/>), given the next flag (the
    /// highest known doubled, RoomManager).
    /// </summary>
    private static int ThemeOf(string list, Dictionary<string, int> themes, bool create)
    {
        int value = 0;
        foreach (string name in list.Split(',').Select(s => s.Trim()).Where(s => s != ""))
        {
            if (create && !themes.ContainsKey(name))
            {
                themes[name] = Math.Max(1, themes.Values.DefaultIfEmpty(0).Max() * 2);
            }
            value += themes.TryGetValue(name, out int v) ? v : int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
        }
        return value;
    }

    /// <summary>Text split as the mod splits a value: at ",", each part trimmed, blank parts left out.</summary>
    private static string[] Split(string text) => [.. text.Split(',').Select(s => s.Trim()).Where(s => s != "")];

    /// <summary>"x, z, y" as the game's x, y, z (Parse.VectorXZY): a part missing or not a number the default's.</summary>
    private static Vector3 VectorXZY(string text, Vector3 fallback)
    {
        string[] s = Split(text);
        return new Vector3(Number(s, 0, fallback.X), Number(s, 2, fallback.Y), Number(s, 1, fallback.Z));
    }

    /// <summary>A number at an index as the mod reads it (Parse.Float): the default when missing or not a number.</summary>
    private static float Number(string[] s, int index, float fallback = 0f) =>
        index < s.Length && float.TryParse(s[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
}
