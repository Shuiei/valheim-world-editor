using System.Runtime.CompilerServices;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

// The dungeons of an open area and their rooms, as they are now (edits included): each dungeon object
// (DG_Cave, DG_SunkenCrypt...) and the room list in its data. Read again after each edit; the 3D view
// draws the rooms and the Dungeon tool edits them.
public static class DungeonRooms
{
	public sealed record Dungeon(int Index, WorldScene.Thing Thing, Dungeons.Kind Kind, List<Dungeons.Placed> Rooms, string? Problem);

	private sealed record Cache(int Version, int Count, IReadOnlyList<Dungeon> Dungeons);

	private static readonly ConditionalWeakTable<WorldScene, Cache> Caches = new();

	// Scenes whose dungeons are being read again for the view.
	private static readonly ConditionalWeakTable<WorldScene, object> Reading = new();

	private static (int Version, int Count) Now(WorldScene s)
	{
		lock (s.Things)
		{
			return (s.Session?.Edits.Version ?? 0, s.Things.Count);
		}
	}

	// For drawing (each frame): what was read last, without waiting; after an edit they are read again on
	// a worker (chunk files, each dungeon's data), and ready is called when they are.
	public static IReadOnlyList<Dungeon> Shown(WorldScene s, Action ready)
	{
		var (version, count) = Now(s);
		Caches.TryGetValue(s, out var c);
		if (c is { } k && k.Version == version && k.Count == count)
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
				finally
				{
					Reading.Remove(s);
				}
				ready();
			});
		}
		return c?.Dungeons ?? Array.Empty<Dungeon>();
	}

	public static IReadOnlyList<Dungeon> Of(WorldScene s)
	{
		var (version, count) = Now(s);
		if (Caches.TryGetValue(s, out var c) && c.Version == version && c.Count == count)
		{
			return c.Dungeons;
		}
		var list = new List<Dungeon>();
		WorldScene.Thing[] things;
		lock (s.Things)
		{
			things = s.Things.ToArray();
		}
		for (int i = 0; i < things.Length; i++)
		{
			var t = things[i];
			if (t.Gone || Dungeons.KindOf(t.Prefab) is not { } kind)
			{
				continue;
			}
			List<Dungeons.Placed> rooms = new();
			string? problem = null;
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
					problem = "The game has not laid this dungeon out yet (nobody has been near it).";
				}
			}
			catch (Exception e) when (e is InvalidDataException or IOException or EndOfStreamException or ArgumentException or FormatException or OverflowException)
			{
				problem = e.Message;
			}
			list.Add(new Dungeon(i, t, kind, rooms, problem));
		}
		Caches.AddOrUpdate(s, new Cache(version, count, list));
		return list;
	}
}
