using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// An object of the world as the editor has it: its prefab, its place as drawn, and its data (the ZDO's values) by
/// key. Positions are in metres, x east, y up, z north; turns are Unity Euler angles in degrees, as the game has them.
/// </summary>
public interface IObjectView
{
    /// <summary>Its prefab's name ("" when the editor does not know it).</summary>
    string Prefab { get; }

    /// <summary>Where it is drawn (for a linked object's part, where its mod builds it, not where the game saves it).</summary>
    Vector3 Position { get; }

    /// <summary>Its turn as drawn, Unity Euler degrees.</summary>
    Vector3 Rotation { get; }

    /// <summary>An int value; false when it has none.</summary>
    bool TryGetInt(string key, out int value);

    /// <summary>A long value; false when it has none.</summary>
    bool TryGetLong(string key, out long value);

    /// <summary>A float value; false when it has none.</summary>
    bool TryGetFloat(string key, out float value);

    /// <summary>A vector value; false when it has none.</summary>
    bool TryGetVector3(string key, out Vector3 value);

    /// <summary>A quaternion value; false when it has none.</summary>
    bool TryGetQuaternion(string key, out Quaternion value);

    /// <summary>A string value; false when it has none.</summary>
    bool TryGetString(string key, out string value);

    /// <summary>A byte-array value; false when it has none.</summary>
    bool TryGetBytes(string key, out byte[] value);
}
