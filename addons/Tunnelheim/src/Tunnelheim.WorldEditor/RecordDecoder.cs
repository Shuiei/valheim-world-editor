using System.Globalization;
using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor;

/// <summary>A column's record in the inspector, in words: its edits, its checkpoint and whether it keeps its ground.</summary>
public sealed class RecordDecoder : IDataDecoder
{
    /// <inheritdoc/>
    public string? Describe(IObjectView obj, byte[] value)
    {
        try
        {
            var r = ColumnRecord.Parse(value);
            return string.Format(CultureInfo.InvariantCulture, "Tunnel record (version {0}): {1} edit(s){2}{3}.", r.Version, r.Ops.Count,
                r.CheckpointId != 0 ? ", from checkpoint " + r.CheckpointId.ToString(CultureInfo.InvariantCulture) : "",
                r.Seeds != null ? ", ground kept" : "");
        }
        catch (InvalidDataException e)
        {
            return "Not a record Tunnelheim 1.0.0 reads: " + e.Message;
        }
    }
}
