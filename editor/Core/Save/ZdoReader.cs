using System.Numerics;

namespace TerrainEditor.Save;

/// <summary>
/// The one reader of a saved object's (ZDO's) layout, mirroring ZDO.Load: the head (flags, position, prefab, rotation)
/// as it is made, then each value in file order through <see cref="Next"/>, one at a time, so a caller keeps only what
/// it needs (the world loader) or every value (<see cref="ZdoData"/>).
/// </summary>
public ref struct ZdoReader
{
    /// <summary>The bytes being read, positioned after the value last given.</summary>
    private readonly ValheimReader _reader;

    /// <summary>The world version the object was written in.</summary>
    private readonly int _worldVersion;

    /// <summary>The section flags not started yet (the connection, then one bit per value type, lowest first).</summary>
    private ushort _sections;

    /// <summary>The values left in the section being read.</summary>
    private int _left;

    /// <summary>Reads an object's head; its values follow through <see cref="Next"/>.</summary>
    /// <param name="reader">Positioned at the object's flags.</param>
    /// <param name="worldVersion">
    /// The world version it was written in (older ones carry a sector and a full rotation).
    /// </param>
    public ZdoReader(ValheimReader reader, int worldVersion)
    {
        _reader = reader;
        _worldVersion = worldVersion;
        bool chunked = worldVersion >= WorldVersion.ChunkedSave;
        Flags = reader.ReadUShort();
        if (!chunked)
        {
            reader.ReadVector2s();
        }
        if ((Flags & ZdoData.SmallPositionFlag) != 0)
        {
            var (x, y) = reader.ReadVector2s();
            Position = new Vector3(x, 0f, y);
            SmallPosition = true;
        }
        else
        {
            Position = reader.ReadVector3();
        }
        Prefab = reader.ReadInt();
        if ((Flags & ZdoData.RotationFlag) != 0)
        {
            Rotation = chunked ? reader.ReadSmallRotation() : reader.ReadVector3();
        }
        DataStart = reader.Position;
        _sections = (ushort)(Flags & 0xFF);
    }

    /// <summary>The ZDO flags as saved (which sections follow, rotation and position form, type bits).</summary>
    public ushort Flags { get; }

    /// <summary>World position in metres.</summary>
    public Vector3 Position { get; }

    /// <summary>Saved in the short form (the whole metres as two shorts, height 0).</summary>
    public bool SmallPosition { get; }

    /// <summary>The prefab name hash.</summary>
    public int Prefab { get; }

    /// <summary>Unity Euler angles in degrees (zero when none is stored).</summary>
    public Vector3 Rotation { get; }

    /// <summary>Where the data sections start in the stream (after position, prefab and rotation).</summary>
    public long DataStart { get; }

    /// <summary>Whether any data section follows the head.</summary>
    public readonly bool HasData => (Flags & 0xFF) != 0;

    /// <summary>
    /// The section flag of the value last given (<see cref="ZdoData.Connections"/>, <see cref="ZdoData.Floats"/>...).
    /// </summary>
    public ushort Section { get; private set; }

    /// <summary>The data key hash of the value last given (0 for the connection).</summary>
    public int Key { get; private set; }

    /// <summary>The connection section's five bytes (type and target), when it was the last given.</summary>
    public byte[]? Connection { get; private set; }

    /// <summary>The value last given, from the floats section.</summary>
    public float FloatValue { get; private set; }

    /// <summary>The value last given, from the vec3 section.</summary>
    public Vector3 Vec3Value { get; private set; }

    /// <summary>The value last given, from the quaternion section.</summary>
    public Quaternion QuatValue { get; private set; }

    /// <summary>The value last given, from the ints section.</summary>
    public int IntValue { get; private set; }

    /// <summary>The value last given, from the longs section.</summary>
    public long LongValue { get; private set; }

    /// <summary>The value last given, from the strings section.</summary>
    public string? StringValue { get; private set; }

    /// <summary>The value last given, from the byte arrays section.</summary>
    public byte[]? BytesValue { get; private set; }

    /// <summary>Where the byte array last given starts in the stream (at its length).</summary>
    public long ValueStart { get; private set; }

    /// <summary>
    /// Reads the next value (the connection first, when there is one): its section, key and value; the stream is then
    /// right after it. False once every section is read.
    /// </summary>
    public bool Next()
    {
        while (_left == 0)
        {
            if (_sections == 0)
            {
                return false;
            }
            ushort flag = (ushort)(_sections & -_sections);
            _sections &= (ushort)~flag;
            Section = flag;
            if (flag == ZdoData.Connections)
            {
                Key = 0;
                Connection = _reader.ReadBytes(5);
                return true;
            }
            _left = _reader.ReadNumItems(_worldVersion);
        }
        _left--;
        Key = _reader.ReadInt();
        switch (Section)
        {
            case ZdoData.Floats:
                FloatValue = _reader.ReadSingle();
                break;
            case ZdoData.Vec3s:
                Vec3Value = _reader.ReadVector3();
                break;
            case ZdoData.Quats:
                QuatValue = _reader.ReadQuaternion();
                break;
            case ZdoData.Ints:
                IntValue = _reader.ReadInt();
                break;
            case ZdoData.Longs:
                LongValue = _reader.ReadLong();
                break;
            case ZdoData.Strings:
                StringValue = _reader.ReadString();
                break;
            default:
                ValueStart = _reader.Position;
                BytesValue = _reader.ReadByteArray();
                break;
        }
        return true;
    }
}
