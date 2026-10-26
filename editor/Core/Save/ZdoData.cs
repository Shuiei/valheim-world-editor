using System.Numerics;

namespace TerrainEditor.Save;

/// <summary>
/// Everything one saved object (ZDO) holds, read into typed lists that can be looked at and changed, then written back
/// in the save format (world versions 41 and 42, which lay objects out the same). The object inspector works on
/// this.
/// </summary>
public sealed partial class ZdoData
{
    /// <summary>Between the numbers of a vector or quaternion typed in the inspector.</summary>
    private static readonly char[] NumberSeparators = [' ', ',', ';'];

    /// <summary>
    /// The ZDO flag bits that say which data sections follow (the connection, then one bit per value type).
    /// </summary>
    public const ushort Connections = 0x1, Floats = 0x2, Vec3s = 0x4, Quats = 0x8, Ints = 0x10, Longs = 0x20, Strings = 0x40, ByteArrays = 0x80;

    /// <summary>ZDO flag bits: a rotation is stored; the position is in the short form (the zone, as two shorts).</summary>
    public const ushort RotationFlag = 0x1000, SmallPositionFlag = 0x2000;

    /// <summary>Vec3 data key of a per-axis scale.</summary>
    public static readonly int ScaleKey = StableHash.Of("scale");

    /// <summary>Float data key of a uniform scale.</summary>
    public static readonly int ScaleScalarKey = StableHash.Of("scaleScalar");

    /// <summary>
    /// The flag bits kept as saved: persistent / distant / object type (0x0F00) and the two the format leaves unused
    /// (0xC000), as ZdoBuilder keeps them.
    /// </summary>
    private const ushort KeptFlags = 0xCF00;

    /// <summary>Persistent / distant / object type bits (0x0F00) and any unused high bits (0xC000), as saved.</summary>
    public ushort BaseFlags { get; set; }

    /// <summary>World position in metres.</summary>
    public Vector3 Position { get; set; }

    /// <summary>
    /// Saved in the game's short form (whole metres as two shorts, height 0), as the game does for objects such as the
    /// terrain compiler: written back the same way while the position still fits.
    /// </summary>
    public bool SmallPosition { get; set; }

    /// <summary>The prefab name hash.</summary>
    public int Prefab { get; set; }

    /// <summary>Unity Euler angles in degrees.</summary>
    public Vector3 Rotation { get; set; }

    /// <summary>The connection section (type and target) kept as it was.</summary>
    public byte[]? Connection { get; set; }

    /// <summary>Float values by key hash, in save order.</summary>
    public List<(int Key, float Value)> FloatList { get; } = [];

    /// <summary>Vector values by key hash, in save order.</summary>
    public List<(int Key, Vector3 Value)> Vec3List { get; } = [];

    /// <summary>Quaternion values by key hash, in save order.</summary>
    public List<(int Key, Quaternion Value)> QuatList { get; } = [];

    /// <summary>Int values by key hash, in save order.</summary>
    public List<(int Key, int Value)> IntList { get; } = [];

    /// <summary>Long values by key hash, in save order.</summary>
    public List<(int Key, long Value)> LongList { get; } = [];

    /// <summary>String values by key hash, in save order.</summary>
    public List<(int Key, string Value)> StringList { get; } = [];

    /// <summary>Byte array values by key hash, in save order.</summary>
    public List<(int Key, byte[] Value)> ByteList { get; } = [];
}
