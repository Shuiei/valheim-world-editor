using System.IO.Compression;
using System.Numerics;

namespace TerrainEditor.Save;

// One object (ZDO) of the save: where its bytes are, and what the editor needs to know about it.
public sealed class ObjectRef
{
	public required ChunkFile File { get; init; }

	public required long Start { get; init; }

	// End of the object, and where its data sections start (after position, prefab and rotation).
	public long End { get; set; }

	public long DataStart { get; set; }

	public ushort Flags { get; set; }

	public int Prefab { get; set; }

	public Vector3 Position { get; set; }

	// Player-built piece (has a creator) / terrain modification object.
	public bool IsPiece { get; set; }

	public bool IsTerrain { get; set; }

	// The object's ZDOID in the running game (live snapshots only).
	public (long User, uint Id) LiveId { get; set; }

	public (int X, int Z) Zone => ((int)MathF.Floor((Position.X + 32f) / 64f), (int)MathF.Floor((Position.Z + 32f) / 64f));
}

// Builds new objects by copying an existing object of the same prefab (so all its data, such as
// health or a creator, stays valid) with a new position, rotation and optionally scale.
public static class ZdoBuilder
{
	private const ushort Connections = 0x1, Floats = 0x2, Vec3 = 0x4, Quats = 0x8, Ints = 0x10, Longs = 0x20, Strings = 0x40, ByteArrays = 0x80;

	private const ushort RotationFlag = 0x1000, SmallPositionFlag = 0x2000;

	private static readonly int ScaleKey = StableHash.Of("scale");

	private static readonly int ScaleScalarKey = StableHash.Of("scaleScalar");

	// euler: Unity Euler angles in degrees. scale <= 0 keeps the template's scale data.
	public static byte[] Build(byte[] source, ObjectRef template, int worldVersion, Vector3 position, Vector3 euler, float scale)
	{
		euler = new Vector3(Wrap(euler.X), Wrap(euler.Y), Wrap(euler.Z));
		bool rotated = euler.LengthSquared() > 1e-6f;
		ushort flags = (ushort)(template.Flags & ~SmallPositionFlag & ~RotationFlag);
		if (rotated)
		{
			flags |= RotationFlag;
		}
		List<Section> sections = ReadSections(source, template, worldVersion);
		if (scale > 0f)
		{
			// The game stores a non-default scale as a Vector3 "scale" (ZNetView.SetLocalScale).
			Section vec = sections.FirstOrDefault(s => s.Flag == Vec3) ?? AddSection(sections, Vec3);
			vec.Set(ScaleKey, Concat(BitConverter.GetBytes(scale), BitConverter.GetBytes(scale), BitConverter.GetBytes(scale)));
			sections.FirstOrDefault(s => s.Flag == Floats)?.Items.RemoveAll(i => i.Key == ScaleScalarKey);
		}
		sections.RemoveAll(s => s.Flag != Connections && s.Items.Count == 0);
		flags = (ushort)(flags & 0xFF00);
		foreach (Section s in sections)
		{
			flags |= s.Flag;
		}
		using MemoryStream ms = new();
		using BinaryWriter w = new(ms);
		w.Write(flags);
		w.Write(position.X); w.Write(position.Y); w.Write(position.Z);
		w.Write(template.Prefab);
		if (rotated)
		{
			WriteSmallRotation(w, euler);
		}
		foreach (Section s in sections.OrderBy(s => s.Flag))
		{
			if (s.Flag == Connections)
			{
				w.Write(s.Raw!);
				continue;
			}
			WriteNumItems(w, s.Items.Count);
			foreach (var (key, value) in s.Items)
			{
				w.Write(key);
				w.Write(value);
			}
		}
		w.Flush();
		return ms.ToArray();
	}

	private static float Wrap(float a)
	{
		a %= 360f;
		return a < 0f ? a + 360f : a;
	}

	// Mirrors ZPackage.WriteSmallRotation / ReadSmallRotation (half degrees, 2 bytes for pure Y).
	private static void WriteSmallRotation(BinaryWriter w, Vector3 e)
	{
		uint x = (uint)MathF.Round(e.X * 2f) % 720, y = (uint)MathF.Round(e.Y * 2f) % 720, z = (uint)MathF.Round(e.Z * 2f) % 720;
		if (x == 0 && z == 0)
		{
			w.Write((ushort)(0x8000 | y));
			return;
		}
		uint v = x | (y << 10) | (z << 20);
		w.Write((ushort)(v >> 16));
		w.Write((ushort)(v & 0xFFFF));
	}

	private static void WriteNumItems(BinaryWriter w, int n)
	{
		if (n < 128)
		{
			w.Write((byte)n);
			return;
		}
		w.Write((byte)((n >> 8) | 0x80));
		w.Write((byte)n);
	}

	private sealed class Section(ushort flag)
	{
		public ushort Flag { get; } = flag;

		public List<(int Key, byte[] Value)> Items { get; } = new();

		public byte[]? Raw { get; set; }

		public void Set(int key, byte[] value)
		{
			int i = Items.FindIndex(t => t.Key == key);
			if (i >= 0)
			{
				Items[i] = (key, value);
			}
			else
			{
				Items.Add((key, value));
			}
		}
	}

	private static Section AddSection(List<Section> sections, ushort flag)
	{
		Section s = new(flag);
		sections.Add(s);
		return s;
	}

	private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

	// The data sections of the template, in file order (same reading as WorldSave.ReadZdo).
	private static List<Section> ReadSections(byte[] source, ObjectRef t, int worldVersion)
	{
		List<Section> result = new();
		if ((t.Flags & 0xFF) == 0)
		{
			return result;
		}
		using ValheimReader pkg = new(new MemoryStream(source, (int)t.DataStart, (int)(t.End - t.DataStart)));
		if ((t.Flags & Connections) != 0)
		{
			result.Add(new Section(Connections) { Raw = pkg.ReadBytes(5) });
		}
		foreach (var (flag, read) in new (ushort, Func<ValheimReader, byte[]>)[]
		{
			(Floats, p => p.ReadBytes(4)),
			(Vec3, p => p.ReadBytes(12)),
			(Quats, p => p.ReadBytes(16)),
			(Ints, p => p.ReadBytes(4)),
			(Longs, p => p.ReadBytes(8)),
			(Strings, p => { string s = p.ReadString(); using MemoryStream m = new(); using BinaryWriter bw = new(m); bw.Write(s); bw.Flush(); return m.ToArray(); }),
			(ByteArrays, p => { byte[] b = p.ReadByteArray(); return Concat(BitConverter.GetBytes(b.Length), b); }),
		})
		{
			if ((t.Flags & flag) == 0)
			{
				continue;
			}
			Section s = new(flag);
			int n = pkg.ReadNumItems(worldVersion);
			for (int i = 0; i < n; i++)
			{
				s.Items.Add((pkg.ReadInt(), read(pkg)));
			}
			result.Add(s);
		}
		return result;
	}
}

// The ZoneSystem part of _main.<n>.db2 (ZoneSystem.Save / Load): which zones the game has already
// generated, and every location instance with whether it has been placed. Everything else in the
// file is kept byte for byte.
public sealed class ZoneDb
{
	public int FileVersion { get; private set; }

	public double NetTime { get; private set; }

	public HashSet<(short X, short Z)> Generated { get; } = new();

	public int LocationVersion { get; set; }

	public List<string> GlobalKeys { get; } = new();

	public bool LocationsGenerated { get; set; }

	public List<(int Hash, Vector3 Position, bool Placed)> Locations { get; } = new();

	private byte[] _tail = Array.Empty<byte>();

	public static ZoneDb Load(string path)
	{
		ZoneDb db = new();
		using BinaryReader r = new(File.OpenRead(path));
		db.FileVersion = r.ReadInt32();
		db.NetTime = r.ReadDouble();
		int len = r.ReadInt32();
		byte[] packed = r.ReadBytes(len);
		db._tail = r.ReadBytes((int)(r.BaseStream.Length - r.BaseStream.Position));
		using GZipStream gz = new(new MemoryStream(packed), CompressionMode.Decompress);
		using MemoryStream raw = new();
		gz.CopyTo(raw);
		db.ReadPackage(raw.ToArray());
		return db;
	}

	// The uncompressed ZoneSystem package, as the live bridge sends it.
	public static ZoneDb FromPackage(byte[] package, double netTime)
	{
		ZoneDb db = new() { FileVersion = 41, NetTime = netTime };
		db.ReadPackage(package);
		return db;
	}

	private void ReadPackage(byte[] package)
	{
		ZoneDb db = this;
		using BinaryReader p = new(new MemoryStream(package));
		int n = p.ReadInt32();
		for (int i = 0; i < n; i++)
		{
			db.Generated.Add((p.ReadInt16(), p.ReadInt16()));
		}
		db.LocationVersion = p.ReadInt32();
		int keys = p.ReadInt32();
		for (int i = 0; i < keys; i++)
		{
			db.GlobalKeys.Add(p.ReadString());
		}
		db.LocationsGenerated = p.ReadBoolean();
		int locs = p.ReadInt32();
		for (int i = 0; i < locs; i++)
		{
			db.Locations.Add((p.ReadInt32(), new Vector3(p.ReadSingle(), p.ReadSingle(), p.ReadSingle()), p.ReadBoolean()));
		}
	}

	public void Save(string path)
	{
		using MemoryStream raw = new();
		using (BinaryWriter p = new(raw, System.Text.Encoding.UTF8, leaveOpen: true))
		{
			p.Write(Generated.Count);
			foreach (var (x, z) in Generated)
			{
				p.Write(x);
				p.Write(z);
			}
			p.Write(LocationVersion);
			p.Write(GlobalKeys.Count);
			foreach (string k in GlobalKeys)
			{
				p.Write(k);
			}
			p.Write(LocationsGenerated);
			p.Write(Locations.Count);
			foreach (var (hash, pos, placed) in Locations)
			{
				p.Write(hash);
				p.Write(pos.X); p.Write(pos.Y); p.Write(pos.Z);
				p.Write(placed);
			}
		}
		using MemoryStream packed = new();
		using (GZipStream gz = new(packed, CompressionLevel.Optimal, leaveOpen: true))
		{
			gz.Write(raw.ToArray());
		}
		using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
		using BinaryWriter w = new(fs);
		w.Write(FileVersion);
		w.Write(NetTime);
		w.Write((int)packed.Length);
		w.Write(packed.ToArray());
		w.Write(_tail);
		w.Flush();
		fs.Flush(flushToDisk: true);
	}

	// Forget that a zone was generated: the game builds it again (vegetation, and every location in it
	// that is marked unplaced) the next time a player comes near.
	public void ResetZone(int zx, int zz)
	{
		Generated.Remove(((short)zx, (short)zz));
		for (int i = 0; i < Locations.Count; i++)
		{
			var (hash, pos, placed) = Locations[i];
			if ((int)MathF.Floor((pos.X + 32f) / 64f) == zx && (int)MathF.Floor((pos.Z + 32f) / 64f) == zz && placed)
			{
				Locations[i] = (hash, pos, false);
			}
		}
	}
}
