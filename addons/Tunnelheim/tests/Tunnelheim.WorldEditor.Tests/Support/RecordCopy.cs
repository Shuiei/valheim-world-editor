using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>What an edit's data setter writes (an ICopy keeping only byte arrays), to read a record the tool made back.</summary>
internal sealed class RecordCopy : FakeObject, ICopy
{
    private readonly Dictionary<string, byte[]> _bytes;

    public RecordCopy(Dictionary<string, byte[]> bytes)
        : base(TunnelheimMod.RecordPrefabName, Vector3.Zero, bytes)
    {
        _bytes = bytes;
    }

    /// <summary>The record written under the mod's key.</summary>
    public ColumnRecord Written => ColumnRecord.Parse(_bytes[TunnelheimMod.RecordKeyName]);

    /// <summary>What a data setter writes.</summary>
    public static RecordCopy Of(Action<ICopy> set)
    {
        var copy = new RecordCopy([]);
        set(copy);
        return copy;
    }

    public void SetBytes(string key, byte[]? value)
    {
        if (value != null)
        {
            _bytes[key] = value;
        }
    }

    public void SetInt(string key, int? value)
    {
    }

    public void SetLong(string key, long? value)
    {
    }

    public void SetFloat(string key, float? value)
    {
    }

    public void SetVector3(string key, Vector3? value)
    {
    }

    public void SetString(string key, string? value)
    {
    }
}
