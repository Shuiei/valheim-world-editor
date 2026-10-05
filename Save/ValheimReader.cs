using System.Numerics;

namespace TerrainEditor.Save;

// Mirrors the read side of Valheim's ZPackage (little-endian BinaryReader plus a few custom encodings).
public sealed class ValheimReader(Stream stream) : IDisposable
{
	private readonly BinaryReader _reader = new(stream);

	public long Position => _reader.BaseStream.Position;

	public byte ReadByte() => _reader.ReadByte();

	public bool ReadBool() => _reader.ReadBoolean();

	public short ReadShort() => _reader.ReadInt16();

	public ushort ReadUShort() => _reader.ReadUInt16();

	public int ReadInt() => _reader.ReadInt32();

	public uint ReadUInt() => _reader.ReadUInt32();

	public long ReadLong() => _reader.ReadInt64();

	public float ReadSingle() => _reader.ReadSingle();

	public string ReadString() => _reader.ReadString();

	public byte[] ReadByteArray() => _reader.ReadBytes(_reader.ReadInt32());

	public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

	public Vector3 ReadVector3() => new(ReadSingle(), ReadSingle(), ReadSingle());

	public void SkipQuaternion() => _reader.BaseStream.Seek(16, SeekOrigin.Current);

	public (short X, short Y) ReadVector2s() => (_reader.ReadInt16(), _reader.ReadInt16());

	// ZPackage.ReadSmallRotation: Euler angles in half degrees; 2 bytes for a pure Y rotation.
	public Vector3 ReadSmallRotation()
	{
		uint num = _reader.ReadUInt16();
		if ((num & 0x8000) != 0)
		{
			return new Vector3(0f, (num & 0x7FFF) * 0.5f, 0f);
		}
		num = (num << 16) | _reader.ReadUInt16();
		return new Vector3(num & 0x3FF, (num >> 10) & 0x3FF, (num >> 20) & 0x3FF) * 0.5f;
	}

	// ZPackage.ReadSmallRotation: 2 bytes for a pure Y rotation, otherwise 4 bytes.
	public void SkipSmallRotation()
	{
		ushort first = _reader.ReadUInt16();
		if ((first & 0x8000) == 0)
		{
			_reader.ReadUInt16();
		}
	}

	// ZPackage.ReadNumItems: 1 byte, or 2 bytes when the high bit is set (world version >= 33).
	public int ReadNumItems(int worldVersion)
	{
		if (worldVersion < WorldVersion.NumItems)
		{
			return _reader.ReadByte();
		}
		int num = _reader.ReadByte();
		if ((num & 0x80) != 0)
		{
			num = ((num & 0x7F) << 8) | _reader.ReadByte();
		}
		return num;
	}

	public void Dispose() => _reader.Dispose();
}

public static class WorldVersion
{
	public const int NumItems = 33;

	public const int ChunkedSave = 40;
}

public static class StableHash
{
	// Valheim's StringExtensionMethods.GetStableHashCode, used for prefab and data keys.
	public static int Of(string str)
	{
		unchecked
		{
			int num = 5381;
			int num2 = num;
			for (int i = 0; i < str.Length && str[i] != 0; i += 2)
			{
				num = ((num << 5) + num) ^ str[i];
				if (i == str.Length - 1 || str[i + 1] == '\0')
				{
					break;
				}
				num2 = ((num2 << 5) + num2) ^ str[i + 1];
			}
			return num + num2 * 1566083941;
		}
	}
}
