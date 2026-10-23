using System.Numerics;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// An object a mod's tool adds or changes (ToolEdit), as the tool sets its values (ICopy): its data, which the editor
/// then saves.
/// </summary>
/// <param name="prefab">Its prefab's name.</param>
/// <param name="position">Where it is.</param>
/// <param name="rotation">Its turn, Unity Euler degrees.</param>
/// <param name="data">Its data, changed in place.</param>
/// <param name="scale">Its size (0 or 1: the prefab's own).</param>
public sealed class DataCopy(string prefab, Vector3 position, Vector3 rotation, ZdoData data, float scale = 1) : ObjectView(prefab, position, rotation, data, scale), ICopy
{
    /// <inheritdoc/>
    public void SetInt(string key, int? value) => Data.SetInt(StableHash.Of(key), value);

    /// <inheritdoc/>
    public void SetLong(string key, long? value) => Data.SetLong(StableHash.Of(key), value);

    /// <inheritdoc/>
    public void SetFloat(string key, float? value) => Data.SetFloat(StableHash.Of(key), value);

    /// <inheritdoc/>
    public void SetVector3(string key, Vector3? value) => Data.SetVec3(StableHash.Of(key), value);

    /// <inheritdoc/>
    public void SetQuaternion(string key, Quaternion? value) => Data.SetQuat(StableHash.Of(key), value);

    /// <inheritdoc/>
    public void SetString(string key, string? value) => Data.SetString(StableHash.Of(key), value);

    /// <inheritdoc/>
    public void SetBytes(string key, byte[]? value) => Data.SetBytesOrNone(StableHash.Of(key), value);
}
