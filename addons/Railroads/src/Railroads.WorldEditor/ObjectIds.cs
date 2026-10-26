using System.Globalization;
using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// Objects named by the game's own object ids (<see cref="RailroadsMod.Keys.Wagons"/>, <see cref="RailroadsMod.Keys.Self"/>):
/// twelve bytes an id, little-endian, the user's 8 then the id's 4. Shown in the inspector as the game writes them.
/// </summary>
public sealed class ObjectIds : IDataDecoder
{
    /// <summary>The bytes of one id.</summary>
    public const int IdBytes = 12;

    /// <summary>How many ids the inspector names before it counts the rest.</summary>
    private const int Named = 4;

    /// <summary>The ids in a value; null when the bytes are not whole ids.</summary>
    public static List<(long User, uint Id)>? Read(byte[] bytes)
    {
        if (bytes.Length % IdBytes != 0)
        {
            return null;
        }
        var ids = new List<(long, uint)>(bytes.Length / IdBytes);
        for (int i = 0; i < bytes.Length; i += IdBytes)
        {
            ids.Add((BitConverter.ToInt64(bytes, i), BitConverter.ToUInt32(bytes, i + 8)));
        }
        return ids;
    }

    /// <summary>Ids as the mod keeps them.</summary>
    public static byte[] Write(IReadOnlyList<(long User, uint Id)> ids)
    {
        byte[] bytes = new byte[ids.Count * IdBytes];
        for (int k = 0; k < ids.Count; k++)
        {
            Array.Copy(BitConverter.GetBytes(ids[k].User), 0, bytes, k * IdBytes, 8);
            Array.Copy(BitConverter.GetBytes(ids[k].Id), 0, bytes, (k * IdBytes) + 8, 4);
        }
        return bytes;
    }

    /// <inheritdoc/>
    public string? Describe(IObjectView obj, byte[] value) => Read(value) switch
    {
        null => null,
        { Count: 0 } => "No object.",
        var ids => string.Format(CultureInfo.InvariantCulture, "{0} object(s): {1}{2}", ids.Count,
            string.Join(", ", ids.Take(Named).Select(i => string.Format(CultureInfo.InvariantCulture, "{0}:{1}", i.User, i.Id))),
            ids.Count > Named ? string.Format(CultureInfo.InvariantCulture, " and {0} more", ids.Count - Named) : ""),
    };
}
