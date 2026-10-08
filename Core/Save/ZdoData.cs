using System.Numerics;
using System.Text;

namespace TerrainEditor.Save;

// Everything one saved object (ZDO) holds, read into typed lists that can be looked at and changed,
// then written back in the save format (world version 41). The object inspector works on this.
public sealed class ZdoData
{
	public const ushort Connections = 0x1, Floats = 0x2, Vec3s = 0x4, Quats = 0x8, Ints = 0x10, Longs = 0x20, Strings = 0x40, ByteArrays = 0x80;

	private const ushort RotationFlag = 0x1000, SmallPositionFlag = 0x2000;

	// Persistent / distant / object type bits (0x0F00) as saved.
	public ushort BaseFlags { get; set; }

	public Vector3 Position { get; set; }

	// Saved in the game's short form (whole metres as two shorts, height 0), as the game does for
	// objects such as the terrain compiler: written back the same way while the position still fits.
	public bool SmallPosition { get; set; }

	public int Prefab { get; set; }

	// Unity Euler angles in degrees.
	public Vector3 Rotation { get; set; }

	// The connection section (type and target) kept as it was.
	public byte[]? Connection { get; set; }

	public List<(int Key, float Value)> FloatList { get; } = new();

	public List<(int Key, Vector3 Value)> Vec3List { get; } = new();

	public List<(int Key, Quaternion Value)> QuatList { get; } = new();

	public List<(int Key, int Value)> IntList { get; } = new();

	public List<(int Key, long Value)> LongList { get; } = new();

	public List<(int Key, string Value)> StringList { get; } = new();

	public List<(int Key, byte[] Value)> ByteList { get; } = new();

	public static ZdoData Parse(byte[] bytes, int worldVersion = 41)
	{
		using ValheimReader r = new(new MemoryStream(bytes));
		ZdoData z = new();
		ushort flags = r.ReadUShort();
		z.BaseFlags = (ushort)(flags & 0x0F00);
		if (worldVersion < WorldVersion.ChunkedSave)
		{
			r.ReadVector2s();
		}
		if ((flags & SmallPositionFlag) != 0)
		{
			var (x, y) = r.ReadVector2s();
			z.Position = new Vector3(x, 0f, y);
			z.SmallPosition = true;
		}
		else
		{
			z.Position = r.ReadVector3();
		}
		z.Prefab = r.ReadInt();
		if ((flags & RotationFlag) != 0)
		{
			z.Rotation = worldVersion >= WorldVersion.ChunkedSave ? r.ReadSmallRotation() : r.ReadVector3();
		}
		if ((flags & 0xFF) == 0)
		{
			return z;
		}
		if ((flags & Connections) != 0)
		{
			z.Connection = r.ReadBytes(5);
		}
		void Each(ushort flag, Action<int> read)
		{
			if ((flags & flag) == 0)
			{
				return;
			}
			int n = r.ReadNumItems(worldVersion);
			for (int i = 0; i < n; i++)
			{
				read(r.ReadInt());
			}
		}
		Each(Floats, k => z.FloatList.Add((k, r.ReadSingle())));
		Each(Vec3s, k => z.Vec3List.Add((k, r.ReadVector3())));
		Each(Quats, k => z.QuatList.Add((k, new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()))));
		Each(Ints, k => z.IntList.Add((k, r.ReadInt())));
		Each(Longs, k => z.LongList.Add((k, r.ReadLong())));
		Each(Strings, k => z.StringList.Add((k, r.ReadString())));
		Each(ByteArrays, k => z.ByteList.Add((k, r.ReadByteArray())));
		return z;
	}

	public byte[] Serialize()
	{
		Vector3 euler = new(ZdoBuilder.Wrap(Rotation.X), ZdoBuilder.Wrap(Rotation.Y), ZdoBuilder.Wrap(Rotation.Z));
		bool rotated = euler.LengthSquared() > 1e-6f;
		ushort flags = (ushort)(BaseFlags & 0x0F00);
		bool small = SmallPosition && Position.Y == 0f && Fits(Position.X) && Fits(Position.Z);
		if (small) flags |= SmallPositionFlag;
		if (rotated) flags |= RotationFlag;
		if (Connection != null) flags |= Connections;
		if (FloatList.Count > 0) flags |= Floats;
		if (Vec3List.Count > 0) flags |= Vec3s;
		if (QuatList.Count > 0) flags |= Quats;
		if (IntList.Count > 0) flags |= Ints;
		if (LongList.Count > 0) flags |= Longs;
		if (StringList.Count > 0) flags |= Strings;
		if (ByteList.Count > 0) flags |= ByteArrays;
		using MemoryStream ms = new();
		using BinaryWriter w = new(ms, Encoding.UTF8);
		w.Write(flags);
		if (small)
		{
			w.Write((short)Position.X);
			w.Write((short)Position.Z);
		}
		else
		{
			w.Write(Position.X); w.Write(Position.Y); w.Write(Position.Z);
		}
		w.Write(Prefab);
		if (rotated)
		{
			ZdoBuilder.WriteSmallRotation(w, euler);
		}
		if (Connection != null)
		{
			w.Write(Connection);
		}
		void Each<T>(List<(int Key, T Value)> list, Action<T> write)
		{
			if (list.Count == 0)
			{
				return;
			}
			ZdoBuilder.WriteNumItems(w, list.Count);
			foreach (var (k, v) in list)
			{
				w.Write(k);
				write(v);
			}
		}
		Each(FloatList, v => w.Write(v));
		Each(Vec3List, v => { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); });
		Each(QuatList, v => { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); w.Write(v.W); });
		Each(IntList, v => w.Write(v));
		Each(LongList, v => w.Write(v));
		Each(StringList, v => w.Write(v));
		Each(ByteList, v => { w.Write(v.Length); w.Write(v); });
		w.Flush();
		return ms.ToArray();
	}

	private static bool Fits(float v) => v == MathF.Round(v) && v >= short.MinValue && v <= short.MaxValue;

	// Sets a value (replacing one with the same key in the same section), or removes it (value null).
	// section: floats, vec3, quats, ints, longs, strings, bytes. Values come as text from the editor:
	// numbers, "x y z" (vec3), "x y z w" (quats), base64 (bytes).
	public void Set(string section, int key, string? value)
	{
		static float F(string s) => float.Parse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
		static float[] Fs(string s, int n)
		{
			float[] v = s.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(F).ToArray();
			return v.Length == n ? v : throw new FormatException($"{n} numbers expected");
		}
		switch (section)
		{
			case "floats": Put(FloatList, key, value, F); break;
			case "vec3": Put(Vec3List, key, value, s => { float[] v = Fs(s, 3); return new Vector3(v[0], v[1], v[2]); }); break;
			case "quats": Put(QuatList, key, value, s => { float[] v = Fs(s, 4); return new Quaternion(v[0], v[1], v[2], v[3]); }); break;
			case "ints": Put(IntList, key, value, s => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture)); break;
			case "longs": Put(LongList, key, value, s => long.Parse(s, System.Globalization.CultureInfo.InvariantCulture)); break;
			case "strings": Put(StringList, key, value, s => s); break;
			case "bytes": Put(ByteList, key, value, Convert.FromBase64String); break;
			default: throw new ArgumentException("unknown section " + section);
		}
	}

	public void SetBytes(int key, byte[] value) => Put(ByteList, key, value, v => v);

	public byte[]? GetBytes(int key) => ByteList.FirstOrDefault(i => i.Key == key).Value;

	private static void Put<T, TIn>(List<(int Key, T Value)> list, int key, TIn? value, Func<TIn, T> parse)
	{
		int i = list.FindIndex(e => e.Key == key);
		if (value == null)
		{
			if (i >= 0)
			{
				list.RemoveAt(i);
			}
			return;
		}
		T v = parse(value);
		if (i >= 0)
		{
			list[i] = (key, v);
		}
		else
		{
			list.Add((key, v));
		}
	}
}
