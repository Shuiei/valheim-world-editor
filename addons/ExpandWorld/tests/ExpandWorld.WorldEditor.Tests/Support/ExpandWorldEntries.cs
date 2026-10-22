namespace ExpandWorld.WorldEditor.Tests;

/// <summary>Entries of Expand World Data's files, as the reader hands them on, for the tests.</summary>
public static class ExpandWorldEntries
{
    /// <summary>One entry with these fields (texts, lists or maps, as the yaml reader gives them).</summary>
    public static ExpandWorldMod.Entry E(params (string Key, object Value)[] fields) => new(fields.ToDictionary(f => f.Key, f => (object?)f.Value));
}
