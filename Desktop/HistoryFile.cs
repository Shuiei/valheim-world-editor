using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// The history kept on disk, so it is there again when the editor is closed and the world opened later
// (history/ of the data folder, one file per world). It is written after each save or Apply live, so its
// steps are what the world holds. Ground points and zones are kept by world position; objects by their
// kind and exact place (their ids change at every save), each with its full data, so one that is gone
// when the world is opened again can be brought back by an undo or redo, chest contents and all.
public static class HistoryFile
{
	private const int Magic = 0x48455756; // "VWEH"
	private const int Version = 1;

	public static string Folder => Path.Combine(AppSettings.DataDir, "history");

	// A world on this computer by its folder; a live one by where it runs, its name and seed.
	public static string KeyOf(WorldSession w) => w.IsLive
		? $"live\n{w.Label}\n{w.World.Name}\n{w.World.Seed}"
		: $"file\n{Path.GetFullPath(w.World.Directory)}";

	public static string PathOf(string key)
	{
		string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant();
		return Path.Combine(Folder, hash + ".history");
	}

	// What a world on disk looks like: its latest save (index file name, time and size). A game that
	// saved the world since makes another. Null for a live world (it changes all the time).
	public static string? Stamp(WorldSession w)
	{
		if (w.IsLive || !Directory.Exists(w.World.Directory))
		{
			return null;
		}
		var latest = Directory.GetFiles(w.World.Directory, "_main.*.chunks")
			.Select(f => (File: f, Parts: Path.GetFileName(f).Split('.')))
			.Where(p => p.Parts.Length == 3 && int.TryParse(p.Parts[1], out _))
			.MaxBy(p => int.Parse(p.Parts[1], System.Globalization.CultureInfo.InvariantCulture)).File;
		if (latest == null)
		{
			return null;
		}
		var info = new FileInfo(latest);
		return $"{info.Name}|{info.LastWriteTimeUtc.Ticks}|{info.Length}";
	}

	public static void Delete(WorldSession w)
	{
		try
		{
			File.Delete(PathOf(KeyOf(w)));
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
	}

	// ---- Writing.

	private sealed record KeptThing(int Prefab, bool Piece, Vector3 Position, Vector3 Rotation, float Scale, byte[]? Bytes);

	public static void Write(WorldSession w, EditSession.Kept k)
	{
		var all = k.Undo.Concat(k.Redo).ToList();
		if (all.Count == 0)
		{
			Delete(w);
			return;
		}
		// Every object the steps need, once: what it is, where, and its data.
		var ids = k.Ids.Values.SelectMany(v => v).Distinct().ToList();
		var found = WorldScene.FindThings(w.World, w.Edits, ids);
		var index = new Dictionary<int, int>();
		var things = new List<KeptThing>();
		foreach (int id in ids)
		{
			if (!found.TryGetValue(id, out var t))
			{
				continue;
			}
			byte[]? bytes = null;
			try
			{
				bytes = ObjectData.Bytes(w.World, w.Edits, id);
			}
			catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
			{
				Console.WriteLine($"History: no data kept for object {id}: {ex.Message}");
			}
			index[id] = things.Count;
			things.Add(new KeptThing(t.Prefab, t.Piece, t.Position, t.Rotation, t.Scale, bytes));
		}
		var order = all.Select((c, i) => (c, i)).ToDictionary(p => p.c, p => p.i);
		string path = PathOf(KeyOf(w));
		Directory.CreateDirectory(Folder);
		string temp = path + ".new";
		using (var file = File.Create(temp))
		using (var gz = new GZipStream(file, CompressionLevel.Fastest))
		using (var o = new BinaryWriter(gz, Encoding.UTF8))
		{
			o.Write(Magic);
			o.Write(Version);
			o.Write(KeyOf(w));
			string? stamp = Stamp(w);
			o.Write(stamp != null);
			o.Write(stamp ?? "");
			o.Write(DateTime.UtcNow.Ticks);
			o.Write(things.Count);
			foreach (var t in things)
			{
				o.Write(t.Prefab);
				o.Write(t.Piece);
				WriteVector(o, t.Position);
				WriteVector(o, t.Rotation);
				o.Write(t.Scale);
				o.Write(t.Bytes?.Length ?? -1);
				if (t.Bytes != null)
				{
					o.Write(t.Bytes);
				}
			}
			o.Write(k.Undo.Count);
			o.Write(k.Redo.Count);
			foreach (var c in all)
			{
				o.Write(c.Label);
				o.Write(c.Time.Ticks);
				o.Write(c.Removed);
				o.Write(c.Applied);
				o.Write(c.RevertOf != null && order.TryGetValue(c.RevertOf, out int r) ? r : -1);
				o.Write(c.Points.Length);
				foreach (int p in c.Points)
				{
					o.Write(p % k.W + k.Ox);
					o.Write(p / k.W + k.Oz);
				}
				WriteState(o, c.Before);
				WriteState(o, c.After);
				o.Write(c.Zones.Count);
				foreach (var (x, z) in c.Zones)
				{
					o.Write(x + k.X0);
					o.Write(z + k.Z0);
				}
				var cids = k.Ids[c];
				var kept = Enumerable.Range(0, cids.Length).Where(n => index.ContainsKey(cids[n])).ToList();
				o.Write(kept.Count);
				foreach (int n in kept)
				{
					o.Write(index[cids[n]]);
					o.Write(c.Things[n].Before);
					o.Write(c.Things[n].After);
				}
				o.Write(c.Resets.Length);
				foreach (var (reset, before, after) in c.Resets)
				{
					o.Write(reset.X);
					o.Write(reset.Z);
					o.Write(reset.KeepBuildings);
					o.Write(reset.Ground);
					o.Write(before);
					o.Write(after);
				}
			}
		}
		File.Move(temp, path, overwrite: true);
	}

	private static void WriteVector(BinaryWriter o, Vector3 v)
	{
		o.Write(v.X);
		o.Write(v.Y);
		o.Write(v.Z);
	}

	private static Vector3 ReadVector(BinaryReader i) => new(i.ReadSingle(), i.ReadSingle(), i.ReadSingle());

	private static void WriteState(BinaryWriter o, Ground.State s)
	{
		foreach (float f in s.Level) o.Write(f);
		foreach (float f in s.Smooth) o.Write(f);
		o.Write(s.Mod);
		foreach (float f in s.Paint) o.Write(f);
		o.Write(s.PMod);
		foreach (float f in s.Lift) o.Write(f);
	}

	private static Ground.State ReadState(BinaryReader i, int n)
	{
		float[] Floats(int count)
		{
			var a = new float[count];
			for (int k = 0; k < count; k++)
			{
				a[k] = i.ReadSingle();
			}
			return a;
		}
		var level = Floats(n);
		var smooth = Floats(n);
		var mod = i.ReadBytes(n);
		var paint = Floats(n * 4);
		var pmod = i.ReadBytes(n);
		var lift = Floats(n);
		return new Ground.State(level, smooth, mod, paint, pmod, lift);
	}

	// ---- Reading.

	// What was kept for this world, fitted to it as it is now: objects found again by kind and place,
	// the others ready to come back. Steps: how many; Changed: the world may differ from when they were
	// kept (live, or a save made since). Null when nothing was kept (or the file cannot be read).
	public sealed record Restored(EditSession.Kept Kept, int Steps, bool Changed, DateTime SavedAt);

	public static Restored? Read(WorldSession w)
	{
		string path = PathOf(KeyOf(w));
		if (!File.Exists(path))
		{
			return null;
		}
		try
		{
			return ReadFile(w, path);
		}
		// A damaged file (cut, or changed by hand: counts, ticks or indices out of range) is left out:
		// the world still opens, without the earlier history.
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException or ArgumentException
			or IndexOutOfRangeException or OutOfMemoryException or KeyNotFoundException or FormatException)
		{
			Console.WriteLine($"History: could not read {path}: {ex.Message}");
			return null;
		}
	}

	private static Restored? ReadFile(WorldSession w, string path)
	{
		using var file = File.OpenRead(path);
		using var gz = new GZipStream(file, CompressionMode.Decompress);
		using var i = new BinaryReader(gz, Encoding.UTF8);
		if (i.ReadInt32() != Magic || i.ReadInt32() != Version || i.ReadString() != KeyOf(w))
		{
			return null;
		}
		bool hasStamp = i.ReadBoolean();
		string stamp = i.ReadString();
		var savedAt = new DateTime(i.ReadInt64(), DateTimeKind.Utc);
		bool changed = !hasStamp || stamp != Stamp(w);
		// The world's objects by kind and place (to the centimetre: a save keeps them exactly).
		static (int, long, long, long) Place(int prefab, Vector3 p) => (prefab, (long)MathF.Round(p.X * 100), (long)MathF.Round(p.Y * 100), (long)MathF.Round(p.Z * 100));
		var world = new Dictionary<(int, long, long, long), Queue<int>>();
		void Add(int id, int prefab, Vector3 p)
		{
			var key = Place(prefab, p);
			if (!world.TryGetValue(key, out var q))
			{
				world[key] = q = new Queue<int>();
			}
			q.Enqueue(id);
		}
		foreach (var (id, prefab, p, _, _) in w.World.Objects)
		{
			Add(id, prefab, p);
		}
		foreach (var (id, prefab, p, _) in w.World.Pieces)
		{
			Add(id, prefab, p);
		}
		int count = i.ReadInt32();
		var ids = new int[count];
		var gone = new List<NewObject>();
		for (int n = 0; n < count; n++)
		{
			int prefab = i.ReadInt32();
			i.ReadBoolean();
			var position = ReadVector(i);
			var rotation = ReadVector(i);
			float scale = i.ReadSingle();
			int length = i.ReadInt32();
			byte[]? bytes = length >= 0 ? i.ReadBytes(length) : null;
			if (world.TryGetValue(Place(prefab, position), out var q) && q.Count > 0)
			{
				ids[n] = q.Dequeue();
			}
			else
			{
				// Not in the world now (deleted, or added and undone): an undo or redo brings it back.
				var o = new NewObject(w.NextId(), prefab, position, rotation, scale, Fresh: bytes == null, Raw: bytes);
				ids[n] = o.Id;
				gone.Add(o);
			}
		}
		int undoCount = i.ReadInt32(), redoCount = i.ReadInt32();
		var changes = new List<(EditSession.Change C, int RevertOf)>();
		var points = new List<(int X, int Z)[]>();
		var thingIds = new List<int[]>();
		for (int n = 0; n < undoCount + redoCount; n++)
		{
			string label = i.ReadString();
			var time = new DateTime(i.ReadInt64(), DateTimeKind.Local);
			bool removed = i.ReadBoolean(), applied = i.ReadBoolean();
			int revertOf = i.ReadInt32();
			int np = i.ReadInt32();
			var pts = new (int X, int Z)[np];
			for (int k = 0; k < np; k++)
			{
				pts[k] = (i.ReadInt32(), i.ReadInt32());
			}
			var before = ReadState(i, np);
			var after = ReadState(i, np);
			int nz = i.ReadInt32();
			var zones = new List<(int X, int Z)>();
			for (int k = 0; k < nz; k++)
			{
				zones.Add((i.ReadInt32(), i.ReadInt32()));
			}
			int nt = i.ReadInt32();
			var things = new (int Index, bool Before, bool After)[nt];
			var tids = new int[nt];
			for (int k = 0; k < nt; k++)
			{
				int t = i.ReadInt32();
				tids[k] = ids[t];
				things[k] = (k, i.ReadBoolean(), i.ReadBoolean());
			}
			int nr = i.ReadInt32();
			var resets = new (ZoneReset, bool, bool)[nr];
			for (int k = 0; k < nr; k++)
			{
				resets[k] = (new ZoneReset(i.ReadInt32(), i.ReadInt32(), i.ReadBoolean(), i.ReadBoolean()), i.ReadBoolean(), i.ReadBoolean());
			}
			// Points are fitted below, once the grid holding all of them is known.
			var c = new EditSession.Change(label, Array.Empty<int>(), before, after, zones)
			{
				Things = things, Resets = resets, Time = time, Removed = removed, Applied = applied || n < undoCount, Earlier = true,
			};
			changes.Add((c, revertOf));
			points.Add(pts);
			thingIds.Add(tids);
		}
		if (changes.Count == 0)
		{
			return null;
		}
		var all = points.SelectMany(p => p).ToList();
		int ox = all.Count > 0 ? all.Min(p => p.X) : 0, oz = all.Count > 0 ? all.Min(p => p.Z) : 0;
		int width = all.Count > 0 ? all.Max(p => p.X) - ox + 1 : 1;
		var fitted = new List<EditSession.Change>();
		var keptIds = new Dictionary<EditSession.Change, int[]>();
		for (int n = 0; n < changes.Count; n++)
		{
			var (c, revertOf) = changes[n];
			var f = c with
			{
				Points = points[n].Select(p => (p.Z - oz) * width + (p.X - ox)).ToArray(),
				RevertOf = revertOf >= 0 && revertOf < n ? fitted[revertOf] : null,
			};
			fitted.Add(f);
			keptIds[f] = thingIds[n];
		}
		w.Edits.KeepGone(gone);
		var kept = new EditSession.Kept(fitted.Take(undoCount).ToList(), fitted.Skip(undoCount).ToList(), ox, oz, width, 0, 0, keptIds);
		return new Restored(kept, changes.Count, changed, savedAt.ToLocalTime());
	}
}
