using System.Runtime.CompilerServices;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The dungeons of an open area and their rooms, as they are now (edits included): each dungeon object
/// (DG_Cave, DG_SunkenCrypt...) and the room list in its data. Read again after each edit; the 3D view
/// draws the rooms and the Dungeon tool edits them.
/// </summary>
public static class DungeonRooms
{
    /// <summary>A dungeon of the area.</summary>
    /// <param name="Index">Its object's index in the scene's things.</param>
    /// <param name="Thing">Its dungeon object.</param>
    /// <param name="Kind">What kind of dungeon it is.</param>
    /// <param name="Rooms">Its rooms, as its data lists them (none when it could not be read).</param>
    /// <param name="Problem">
    /// Why its rooms could not be read (or are not laid out yet), for the user and in English for Claude; null when they
    /// were.
    /// </param>
    public sealed record Dungeon(int Index, WorldScene.Thing Thing, Dungeons.Kind Kind, List<Dungeons.Placed> Rooms, Said? Problem);

    /// <summary>The dungeons read for a scene, with the state they were read at (see <see cref="Now"/>).</summary>
    private sealed record Cache((int Version, int Generation, int Count) At, IReadOnlyList<Dungeon> Dungeons);

    /// <summary>What was read last, per scene.</summary>
    private static readonly ConditionalWeakTable<WorldScene, Cache> Caches = [];

    /// <summary>Scenes whose dungeons are being read again for the view.</summary>
    private static readonly ConditionalWeakTable<WorldScene, object> Reading = [];

    /// <summary>
    /// The scene's edit version, its session's generation and its object count: when any changes, the dungeons are read
    /// again. The generation moves when the live game changes things in place (TakeGameChanges: a dungeon laid out or
    /// gone in game), which touches neither the edits nor the count.
    /// </summary>
    private static (int Version, int Generation, int Count) Now(WorldScene s)
    {
        lock (s.Things)
        {
            return (s.Session?.Edits.Version ?? 0, s.Session?.Generation ?? 0, s.Things.Count);
        }
    }

    /// <summary>
    /// For drawing (each frame): what was read last, without waiting; after an edit they are read again on
    /// a worker (chunk files, each dungeon's data), and ready is called when they are (or the read failed: logged).
    /// </summary>
    public static IReadOnlyList<Dungeon> Shown(WorldScene s, Action ready)
    {
        var now = Now(s);
        Caches.TryGetValue(s, out var c);
        if (c is { } k && k.At == now)
        {
            return k.Dungeons;
        }
        if (Reading.TryAdd(s, new object()))
        {
            Task.Run(() =>
            {
                try
                {
                    Of(s);
                }
                catch (Exception e)
                {
                    // Logged, and what was shown kept for this state: a read failing the same way is not started again
                    // on every frame.
                    Console.Error.WriteLine($"The dungeons' rooms could not be read: {e}");
                    Caches.AddOrUpdate(s, new Cache(now, c?.Dungeons ?? []));
                }
                finally
                {
                    Reading.Remove(s);
                }
                ready();
            });
        }
        return c?.Dungeons ?? [];
    }

    /// <summary>The scene's dungeons as they are now, read when the cache is out of date (on the caller's thread).</summary>
    public static IReadOnlyList<Dungeon> Of(WorldScene s)
    {
        var now = Now(s);
        if (Caches.TryGetValue(s, out var c) && c.At == now)
        {
            return c.Dungeons;
        }
        var list = new List<Dungeon>();
        WorldScene.Thing[] things;
        lock (s.Things)
        {
            things = [.. s.Things];
        }
        for (int i = 0; i < things.Length; i++)
        {
            var t = things[i];
            if (t.Gone || Dungeons.KindOf(t.Prefab) is not { } kind)
            {
                continue;
            }
            List<Dungeons.Placed> rooms = [];
            Said? problem = null;
            try
            {
                byte[]? bytes = s.Session != null ? ObjectData.Bytes(s.World, s.Session.Edits, t.Id) : t.Id >= 0 ? s.World.ObjectBytes(t.Id) : null;
                byte[]? data = bytes == null ? null : Save.ZdoData.Parse(bytes).GetBytes(Dungeons.RoomDataKey);
                if (data != null)
                {
                    rooms = Dungeons.Read(data);
                }
                else
                {
                    problem = Said.Of("dungeon.error.notLaidOut");
                }
            }
            catch (Exception e) when (e is InvalidDataException or IOException or EndOfStreamException or ArgumentException or FormatException or OverflowException)
            {
                problem = Said.Raw(e.Message);
            }
            list.Add(new Dungeon(i, t, kind, rooms, problem));
        }
        Caches.AddOrUpdate(s, new Cache(now, list));
        return list;
    }
}
