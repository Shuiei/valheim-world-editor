using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using TerrainEditor.Save;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.Editing;

// The game's dungeons (Frost Caves, crypts, Dvergr mines...) as the editor's Dungeon tool edits them.
// A dungeon is one object (DG_Cave, DG_SunkenCrypt...) whose byte array "roomData" lists its rooms: the
// stable hash of each room prefab's name, its position and rotation. The game builds the rooms from
// that list when the dungeon loads (DungeonGenerator.Load/Spawn) and never checks it against its own
// generator, so a list made here is built as it is. Rooms and their openings come from
// dungeon-rooms.json.gz (tools/asset-export/scan_dungeon_rooms.py). All in Unity's world space.
public static class Dungeons
{
	public static readonly int RoomDataKey = StableHash.Of("roomData");

	// One opening of a room, in the room's own frame. Two rooms join where openings of the same type
	// meet at one point, facing each other.
	public sealed record Opening(string Type, bool Entrance, bool AllowDoor, Vector3 Position, Quaternion Rotation);

	// A networked object the game makes with a room when it first generates it; Node is where it sits in
	// the room's tree of random parts (-1: on the room itself).
	public sealed record Content(string Prefab, Vector3 Position, Quaternion Rotation, int Node);

	// RandomSpawn: the node is there with this chance (%) when the dungeon has the theme (0: any) and it
	// is within the heights; when not, Off (a node, -1 none) is shown instead.
	public sealed record Spawn(int Node, float Chance, int Theme, int Off, int MinY, int MaxY);

	// RandomObject: one of the choices (node, weight) is kept, the others are not.
	public sealed record Pick(int Node, int Theme, (int Node, float Weight)[] Choices, int MinY, int MaxY);

	public sealed record Room(string Name, int Hash, int Theme, Vector3 Size, bool EndCap, bool Entrance, bool Divider, int EndCapPrio,
		float Weight, bool Perimeter, bool Enabled, Opening[] Openings, Content[] Contents, Spawn[] Spawns, Pick[] Picks, int[] Nodes);

	// A kind of dungeon (the DG_ prefab): its room themes; Interior: built in the sky above its entrance
	// (DungeonGenerator.Algorithm.Dungeon), not laid out on the ground (camps, villages); ZoneSize: the
	// box its rooms stay in, around the zone's centre at the dungeon's height.
	// CustomInterior: rooms are rolled by their place relative to the dungeon (m_useCustomInteriorTransform);
	// BaseSeed: the dungeon's seed is added to each room's roll (m_addBaseSeedToRandomSpawn).
	public sealed record Kind(string Name, int Hash, int Themes, bool Interior, Vector3 ZoneSize, bool CustomInterior = false, bool BaseSeed = false);

	private sealed record Catalog(Dictionary<int, Room> Rooms, Dictionary<int, Kind> Kinds);

	private static readonly Lazy<Catalog> All = new(Load);

	public static Room? RoomOf(int hash) => All.Value.Rooms.GetValueOrDefault(hash);

	public static Room? RoomOf(string name) => RoomOf(StableHash.Of(name));

	public static Kind? KindOf(int prefab) => All.Value.Kinds.GetValueOrDefault(prefab);

	public static IEnumerable<Kind> Kinds => All.Value.Kinds.Values.OrderBy(k => k.Name, StringComparer.Ordinal);

	public static IEnumerable<Room> AllRooms => All.Value.Rooms.Values.OrderBy(r => r.Name, StringComparer.Ordinal);

	// The rooms a dungeon of this kind can hold: those of its themes, the game's way
	// (DungeonGenerator.SetupAvailableRooms: enabled rooms whose theme shares a flag with the dungeon's).
	public static IEnumerable<Room> RoomsFor(Kind kind) => AllRooms.Where(r => r.Enabled && (r.Theme & kind.Themes) != 0);

	private static Catalog Load()
	{
		using Stream gz = typeof(Dungeons).Assembly.GetManifestResourceStream("TerrainEditor.dungeon-rooms.json.gz")
			?? throw new InvalidOperationException("dungeon-rooms.json.gz is not embedded");
		using var s = new GZipStream(gz, CompressionMode.Decompress);
		using JsonDocument doc = JsonDocument.Parse(s);
		static Vector3 V(JsonElement a, int i) => new(a[i].GetSingle(), a[i + 1].GetSingle(), a[i + 2].GetSingle());
		static Quaternion Q(JsonElement a, int i) => new(a[i].GetSingle(), a[i + 1].GetSingle(), a[i + 2].GetSingle(), a[i + 3].GetSingle());
		var rooms = new Dictionary<int, Room>();
		foreach (JsonProperty p in doc.RootElement.GetProperty("rooms").EnumerateObject())
		{
			JsonElement r = p.Value;
			bool B(string k) => r.GetProperty(k).GetInt32() != 0;
			rooms[StableHash.Of(p.Name)] = new Room(p.Name, StableHash.Of(p.Name), r.GetProperty("theme").GetInt32(), V(r.GetProperty("size"), 0),
				B("endCap"), B("entrance"), B("divider"), r.GetProperty("endCapPrio").GetInt32(), r.GetProperty("weight").GetSingle(), B("perimeter"), B("enabled"),
				r.GetProperty("openings").EnumerateArray().Select(o => new Opening(o[0].GetString() ?? "", o[1].GetInt32() != 0, o[2].GetInt32() != 0, V(o, 4), Q(o, 7))).ToArray(),
				r.GetProperty("contents").EnumerateArray().Select(c => new Content(c[0].GetString() ?? "", V(c, 1), Q(c, 4), c[8].GetInt32())).ToArray(),
				r.GetProperty("spawns").EnumerateArray().Select(x => new Spawn(x[0].GetInt32(), x[1].GetSingle(), x[2].GetInt32(), x[3].GetInt32(), x[4].GetInt32(), x[5].GetInt32())).ToArray(),
				r.GetProperty("picks").EnumerateArray().Select(x => new Pick(x[0].GetInt32(), x[1].GetInt32(),
					x[2].EnumerateArray().Select(c => (c[0].GetInt32(), c[1].GetSingle())).ToArray(), x[3].GetInt32(), x[4].GetInt32())).ToArray(),
				r.GetProperty("nodes").EnumerateArray().Select(n => n.GetInt32()).ToArray());
		}
		var kinds = new Dictionary<int, Kind>();
		foreach (JsonProperty p in doc.RootElement.GetProperty("dungeons").EnumerateObject())
		{
			JsonElement d = p.Value;
			kinds[StableHash.Of(p.Name)] = new Kind(p.Name, StableHash.Of(p.Name), d.GetProperty("themes").GetInt32(), d.GetProperty("algorithm").GetInt32() == 0, V(d.GetProperty("zoneSize"), 0),
				d.GetProperty("customInterior").GetInt32() != 0, d.GetProperty("baseSeed").GetInt32() != 0);
		}
		return new Catalog(rooms, kinds);
	}

	// ---- Rotations: the save keeps Euler angles (Unity's eulerAngles, degrees; Quaternion.Euler turns
	// about z, then x, then y).
	private const float Deg = MathF.PI / 180f;

	public static Quaternion FromEuler(Vector3 e) => Quaternion.CreateFromYawPitchRoll(e.Y * Deg, e.X * Deg, e.Z * Deg);

	public static Vector3 ToEuler(Quaternion q)
	{
		q = Quaternion.Normalize(q);
		// Unity's Quaternion.eulerAngles for its ZXY order: pitch from the rotated forward's height.
		float sinX = 2f * (q.W * q.X - q.Y * q.Z);
		float x, y, z;
		if (MathF.Abs(sinX) > 0.9999f)
		{
			// Looking straight up or down: z folded into y.
			x = MathF.CopySign(90f, sinX);
			y = MathF.Atan2(2f * (q.W * q.Y - q.X * q.Z), 1f - 2f * (q.Y * q.Y + q.Z * q.Z)) / Deg;
			z = 0;
		}
		else
		{
			x = MathF.Asin(sinX) / Deg;
			y = MathF.Atan2(2f * (q.W * q.Y + q.X * q.Z), 1f - 2f * (q.X * q.X + q.Y * q.Y)) / Deg;
			z = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y), 1f - 2f * (q.X * q.X + q.Z * q.Z)) / Deg;
		}
		static float Wrap(float a) => a < 0 ? a + 360f : a >= 360f ? a - 360f : a;
		return new Vector3(Wrap(x), Wrap(y), Wrap(z));
	}

	// ---- The room list.
	public sealed record Placed(int Hash, Vector3 Position, Quaternion Rotation)
	{
		public Room? Room => RoomOf(Hash);

		public string Name => Room?.Name ?? Hash.ToString(System.Globalization.CultureInfo.InvariantCulture);

		// An opening in world space: where it is and which way it faces.
		public (Vector3 Position, Quaternion Rotation) At(Opening o) => (Position + Vector3.Transform(o.Position, Rotation), Rotation * o.Rotation);

		public Vector3 ToWorld(Vector3 local) => Position + Vector3.Transform(local, Rotation);
	}

	// DungeonGenerator.Save: an int count, then per room its hash (int), position (3 floats) and
	// rotation (Euler angles, 3 floats).
	public static List<Placed> Read(byte[] data)
	{
		using var r = new BinaryReader(new MemoryStream(data));
		int n = r.ReadInt32();
		if (n < 0 || n > (data.Length - 4) / 28)
		{
			throw new InvalidDataException($"The dungeon's room list is damaged ({n} rooms in {data.Length} bytes).");
		}
		var list = new List<Placed>(n);
		for (int i = 0; i < n; i++)
		{
			int hash = r.ReadInt32();
			var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
			var euler = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
			list.Add(new Placed(hash, pos, FromEuler(euler)));
		}
		return list;
	}

	public static byte[] Write(IReadOnlyList<Placed> rooms)
	{
		var ms = new MemoryStream(4 + rooms.Count * 28);
		using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
		{
			w.Write(rooms.Count);
			foreach (Placed p in rooms)
			{
				Vector3 e = ToEuler(p.Rotation);
				w.Write(p.Hash);
				w.Write(p.Position.X);
				w.Write(p.Position.Y);
				w.Write(p.Position.Z);
				w.Write(e.X);
				w.Write(e.Y);
				w.Write(e.Z);
			}
		}
		return ms.ToArray();
	}

	// ---- Joining rooms, the game's way (DungeonGenerator.PlaceRoom + CalculateRoomPosRot): the new
	// room is turned so that its opening `mine` faces the other way from the open one, and moved so the
	// two meet.
	public static Placed Attach(Room room, Opening mine, Vector3 openPosition, Quaternion openRotation)
	{
		Quaternion exit = openRotation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
		Quaternion rot = exit * Quaternion.Inverse(mine.Rotation);
		Vector3 pos = openPosition - Vector3.Transform(mine.Position, rot);
		return new Placed(room.Hash, pos, Quaternion.Normalize(rot));
	}

	// The openings a room can join an open one of this type with: those of the same type (an end cap's
	// only opening too), never the entrance's way in.
	public static IEnumerable<Opening> Matching(Room room, string type) => room.Openings.Where(o => !o.Entrance && o.Type == type);

	// An opening of a placed room and whether another room meets it there (RoomConnection.TestContact:
	// within 0.1 m).
	public sealed record OpenEnd(int Room, int Index, Vector3 Position, Quaternion Rotation, string Type, bool Entrance);

	public static List<OpenEnd> Openings(IReadOnlyList<Placed> rooms, bool freeOnly)
	{
		var all = new List<OpenEnd>();
		for (int i = 0; i < rooms.Count; i++)
		{
			Room? room = rooms[i].Room;
			if (room == null)
			{
				continue;
			}
			for (int k = 0; k < room.Openings.Length; k++)
			{
				var (p, q) = rooms[i].At(room.Openings[k]);
				all.Add(new OpenEnd(i, k, p, q, room.Openings[k].Type, room.Openings[k].Entrance));
			}
		}
		if (!freeOnly)
		{
			return all;
		}
		// The entrance room's way in leads out of the dungeon: it never takes a room.
		return all.Where(a => !a.Entrance && !all.Any(b => b.Room != a.Room && Vector3.DistanceSquared(a.Position, b.Position) < 0.01f)).ToList();
	}

	// ---- Room boxes (Room.m_size, centred on the room's position, turned with it), for picking and for
	// the game's overlap test (DungeonGenerator.TestCollision: the new room's box 0.1 m smaller).
	public static (Vector3 Center, Vector3 Half, Quaternion Rotation) Box(Placed p)
	{
		Vector3 size = p.Room?.Size ?? new Vector3(4);
		return (p.Position, size / 2f, p.Rotation);
	}

	// Indexes of the placed rooms the box of `p` (at index `skip`, -1 for a new one) overlaps.
	public static List<int> Overlaps(IReadOnlyList<Placed> rooms, Placed p, int skip = -1)
	{
		var hits = new List<int>();
		Room? room = p.Room;
		if (room == null || room.Size.X == 0 || room.Size.Z == 0)
		{
			return hits;
		}
		var a = (p.Position, (room.Size - new Vector3(0.1f)) / 2f, p.Rotation);
		for (int i = 0; i < rooms.Count; i++)
		{
			if (i == skip || rooms[i].Room is not { } other || other.Size.X == 0 || other.Size.Z == 0)
			{
				continue;
			}
			if (BoxesOverlap(a, (rooms[i].Position, other.Size / 2f, rooms[i].Rotation)))
			{
				hits.Add(i);
			}
		}
		return hits;
	}

	// Separating axis test of two turned boxes (centre, half size, rotation).
	public static bool BoxesOverlap((Vector3 C, Vector3 H, Quaternion R) a, (Vector3 C, Vector3 H, Quaternion R) b)
	{
		Vector3[] ax = { Vector3.Transform(Vector3.UnitX, a.R), Vector3.Transform(Vector3.UnitY, a.R), Vector3.Transform(Vector3.UnitZ, a.R) };
		Vector3[] bx = { Vector3.Transform(Vector3.UnitX, b.R), Vector3.Transform(Vector3.UnitY, b.R), Vector3.Transform(Vector3.UnitZ, b.R) };
		Vector3 d = b.C - a.C;
		float[] ha = { a.H.X, a.H.Y, a.H.Z }, hb = { b.H.X, b.H.Y, b.H.Z };
		bool Separated(Vector3 axis)
		{
			if (axis.LengthSquared() < 1e-8f)
			{
				return false;
			}
			float ra = 0, rb = 0;
			for (int i = 0; i < 3; i++)
			{
				ra += ha[i] * MathF.Abs(Vector3.Dot(ax[i], axis));
				rb += hb[i] * MathF.Abs(Vector3.Dot(bx[i], axis));
			}
			// Touching faces do not overlap (rooms that join share a wall plane).
			return MathF.Abs(Vector3.Dot(d, axis)) >= ra + rb - 1e-3f * axis.Length();
		}
		for (int i = 0; i < 3; i++)
		{
			if (Separated(ax[i]) || Separated(bx[i]))
			{
				return false;
			}
		}
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				if (Separated(Vector3.Cross(ax[i], bx[j])))
				{
					return false;
				}
			}
		}
		return true;
	}

	// Whether a room stays inside the dungeon's box (DungeonGenerator.IsInsideDungeon: all eight corners
	// of its box within m_zoneSize around the zone's centre, at the dungeon's height).
	public static bool Inside(Kind kind, Vector3 dungeonPosition, Placed p)
	{
		if (p.Room is not { } room)
		{
			return true;
		}
		const float zone = 64f;
		var center = new Vector3(MathF.Floor((dungeonPosition.X + zone / 2) / zone) * zone, dungeonPosition.Y, MathF.Floor((dungeonPosition.Z + zone / 2) / zone) * zone);
		Vector3 min = center - kind.ZoneSize / 2, max = center + kind.ZoneSize / 2;
		Vector3 h = room.Size / 2;
		foreach (float sx in new[] { -1f, 1f })
		{
			foreach (float sy in new[] { -1f, 1f })
			{
				foreach (float sz in new[] { -1f, 1f })
				{
					Vector3 c = p.ToWorld(new Vector3(sx * h.X, sy * h.Y, sz * h.Z));
					if (c.X < min.X || c.Y < min.Y || c.Z < min.Z || c.X > max.X || c.Y > max.Y || c.Z > max.Z)
					{
						return false;
					}
				}
			}
		}
		return true;
	}

	// ---- What the game makes with a room when it generates it (DungeonGenerator.PlaceRoom, SpawnMode
	// Full): its networked objects, those its random parts keep, rolled from the room's place the game's
	// way (Random.InitState, then each RandomSpawn's chance, then each RandomObject's pick, in order). The
	// random parts' own heights are taken as the room's.
	public sealed record Made(string Prefab, Vector3 Position, Quaternion Rotation);

	// DungeonGenerator.GetSeed: the world's seed and the dungeon's zone and place.
	public static int Seed(int worldSeed, Vector3 dungeonPosition)
	{
		int zx = (int)MathF.Floor((dungeonPosition.X + 32f) / 64f), zz = (int)MathF.Floor((dungeonPosition.Z + 32f) / 64f);
		return unchecked(worldSeed + zx * 4271 + zz * -7187 + (int)dungeonPosition.X * -4271 + (int)dungeonPosition.Y * 9187 + (int)dungeonPosition.Z * -2134);
	}

	public static List<Made> Contents(Placed p, Kind kind, Vector3 dungeonPosition, int worldSeed)
	{
		var made = new List<Made>();
		if (p.Room is not { } room || room.Contents.Length == 0)
		{
			return made;
		}
		Vector3 v = kind.CustomInterior ? p.Position - dungeonPosition : p.Position;
		int seed = unchecked((int)v.X * 4271 + (int)v.Y * 9187 + (int)v.Z * 2134 + (kind.BaseSeed ? Seed(worldSeed, dungeonPosition) : 0));
		var off = new bool[room.Nodes.Length];
		var was = Rnd.state;
		try
		{
			Rnd.InitState(seed);
			float y = p.Position.Y;
			foreach (var s in room.Spawns)
			{
				bool spawned = Rnd.Range(0f, 100f) <= s.Chance;
				if (s.Theme != 0 && (kind.Themes & s.Theme) == 0 || y < s.MinY || y > s.MaxY)
				{
					spawned = false;
				}
				Set(s.Node, !spawned);
				if (s.Off >= 0)
				{
					Set(s.Off, spawned);
				}
			}
			foreach (var pick in room.Picks)
			{
				float total = pick.Choices.Sum(c => c.Weight), roll = Rnd.Range(0f, total), sum = 0;
				int? chosen = null;
				foreach (var (node, weight) in pick.Choices)
				{
					sum += weight;
					if (roll <= sum)
					{
						chosen = node;
						break;
					}
				}
				if (pick.Theme != 0 && (kind.Themes & pick.Theme) == 0 || y < pick.MinY || y > pick.MaxY)
				{
					chosen = null;
				}
				foreach (var (node, _) in pick.Choices)
				{
					Set(node, node != chosen);
				}
				if (chosen == null)
				{
					Set(pick.Node, true);
				}
			}
		}
		finally
		{
			Rnd.state = was;
		}
		foreach (var c in room.Contents)
		{
			bool shown = true;
			for (int n = c.Node; n >= 0 && shown; n = room.Nodes[n])
			{
				shown = !off[n];
			}
			if (shown)
			{
				made.Add(new Made(c.Prefab, p.ToWorld(c.Position), Quaternion.Normalize(p.Rotation * c.Rotation)));
			}
		}
		return made;

		void Set(int node, bool hidden)
		{
			if (node >= 0 && node < off.Length)
			{
				off[node] = hidden;
			}
		}
	}

	// ---- Dungeons of a save.
	public sealed record Found(int Id, Kind Kind, Vector3 Position, int Rooms);

	public static List<Found> In(WorldSave world)
	{
		var found = new List<Found>();
		foreach (var o in world.Objects)
		{
			if (KindOf(o.Prefab) is not { } kind)
			{
				continue;
			}
			int count = 0;
			try
			{
				byte[]? data = ZdoData.Parse(world.ObjectBytes(o.Id)).GetBytes(RoomDataKey);
				count = data == null ? 0 : BitConverter.ToInt32(data, 0);
			}
			catch (Exception e) when (e is InvalidDataException or ArgumentException or EndOfStreamException)
			{
			}
			found.Add(new Found(o.Id, kind, o.Position, count));
		}
		return found;
	}

	// The dungeon object's bytes with another room list.
	public static byte[] WithRooms(byte[] dungeonObject, IReadOnlyList<Placed> rooms)
	{
		ZdoData z = ZdoData.Parse(dungeonObject);
		z.SetBytes(RoomDataKey, Write(rooms));
		return z.Serialize();
	}
}
