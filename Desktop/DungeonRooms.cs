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

	public static IReadOnlyList<Dungeon> Of(WorldScene s)
	{
		int version = s.Session?.Edits.Version ?? 0;
		int count;
		lock (s.Things)
		{
			count = s.Things.Count;
		}
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
			catch (Exception e) when (e is InvalidDataException or IOException or EndOfStreamException or ArgumentException)
			{
				problem = e.Message;
			}
			list.Add(new Dungeon(i, t, kind, rooms, problem));
		}
		Caches.AddOrUpdate(s, new Cache(version, count, list));
		return list;
	}
}
