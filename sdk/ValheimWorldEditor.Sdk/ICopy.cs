using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// One object before it is made or changed: a pasted copy's (its values as they will be, the editor's linking already
/// done), or one a tool adds or changes (<see cref="ToolEdit"/>), whose values the extension sets. Setting null takes a
/// value away.
/// </summary>
public interface ICopy : IObjectView
{
    /// <summary>Sets an int value (null: takes it away).</summary>
    void SetInt(string key, int? value);

    /// <summary>Sets a long value (null: takes it away).</summary>
    void SetLong(string key, long? value);

    /// <summary>Sets a float value (null: takes it away).</summary>
    void SetFloat(string key, float? value);

    /// <summary>Sets a vector value (null: takes it away).</summary>
    void SetVector3(string key, Vector3? value);

    /// <summary>Sets a string value (null: takes it away).</summary>
    void SetString(string key, string? value);

    /// <summary>Sets a byte-array value (null: takes it away).</summary>
    void SetBytes(string key, byte[]? value);
}
