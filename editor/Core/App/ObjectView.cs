using System.Numerics;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>An object as extensions see it (IObjectView): its data as read, its place as drawn.</summary>
/// <param name="prefab">Its prefab's name ("" when not known).</param>
/// <param name="position">Where it is drawn.</param>
/// <param name="rotation">Its turn as drawn, Unity Euler degrees.</param>
/// <param name="data">Its data.</param>
/// <param name="scale">Its size (0 or 1: the prefab's own).</param>
public class ObjectView(string prefab, Vector3 position, Vector3 rotation, ZdoData data, float scale = 1) : PlacedView(prefab, position, rotation, scale), IObjectView
{
    /// <summary>
    /// The object's id in the editor (its save id, or the negative id of one added in this session) when it was read
    /// for a tool, which hands it back to change or remove it; null otherwise.
    /// </summary>
    public int? Id { get; init; }

    /// <summary>Its data.</summary>
    public ZdoData Data => data;

    /// <summary>The first value of a key in a list of values.</summary>
    private static bool Find<T>(List<(int Key, T Value)> list, string key, out T value) => ZdoData.TryGet(list, StableHash.Of(key), out value);

    /// <inheritdoc/>
    public bool TryGetInt(string key, out int value) => Find(data.IntList, key, out value);

    /// <inheritdoc/>
    public bool TryGetLong(string key, out long value) => Find(data.LongList, key, out value);

    /// <inheritdoc/>
    public bool TryGetFloat(string key, out float value) => Find(data.FloatList, key, out value);

    /// <inheritdoc/>
    public bool TryGetVector3(string key, out Vector3 value) => Find(data.Vec3List, key, out value);

    /// <inheritdoc/>
    public bool TryGetQuaternion(string key, out Quaternion value) => Find(data.QuatList, key, out value);

    /// <inheritdoc/>
    public bool TryGetString(string key, out string value) => Find(data.StringList, key, out value);

    /// <inheritdoc/>
    public bool TryGetBytes(string key, out byte[] value) => Find(data.ByteList, key, out value);
}
