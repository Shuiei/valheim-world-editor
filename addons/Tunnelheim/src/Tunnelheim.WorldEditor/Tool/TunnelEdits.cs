using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// Edits made the mod's records' way (VoxelWorld.SaveOp): each edit is played on the area's rock and kept by every
/// column whose rock it changed, appended to that column's record; a column without one (or whose record the mod
/// cannot read, which it never writes over) gets a new record holding the ground it was first dug from. Each record
/// then replays only its own column's part.
/// </summary>
public static class TunnelEdits
{
    /// <summary>What a list of edits comes to: the records changed and made, and how many columns that is.</summary>
    /// <param name="Edit">The editor's edit (records changed or added); null when nothing changed.</param>
    /// <param name="Columns">How many columns keep an edit.</param>
    public sealed record Result(ToolEdit? Edit, int Columns);

    /// <summary>Plays the edits on an area's rock and turns what changed into records.</summary>
    /// <param name="area">The area, its records as they are with the edits not saved yet.</param>
    /// <param name="grid">The area's rock as built from them (<see cref="TunnelArea.Build"/>); the edits are played on it.</param>
    /// <param name="ops">The edits, in order.</param>
    /// <param name="log">Where a record the mod cannot read is told.</param>
    public static Result Make(IAreaView area, RockGrid grid, IReadOnlyList<DigOp> ops, Action<string> log)
    {
        var kept = new Dictionary<(int X, int Z), List<DigOp>>();
        foreach (var op in ops)
        {
            var changed = new HashSet<(int X, int Z)>();
            grid.Apply(op, null, changed);
            foreach (var column in changed)
            {
                (kept.TryGetValue(column, out var list) ? list : kept[column] = []).Add(op);
            }
        }
        if (kept.Count == 0)
        {
            return new Result(null, 0);
        }
        var records = Records(area, log);
        var edit = new ToolEdit();
        foreach (var entry in kept)
        {
            var (column, added) = (entry.Key, entry.Value);
            if (records.TryGetValue(column, out var held))
            {
                var updated = held.Record with { Ops = [.. held.Record.Ops, .. added] };
                edit.Change(held.Object, c => c.SetBytes(TunnelheimMod.RecordKeyName, updated.Encode()));
            }
            else
            {
                AddRecord(edit, grid, column, added);
            }
        }
        return new Result(edit, kept.Count);
    }

    /// <summary>A new record for a column: the ground it was first dug from (its seeds) and its edits, in its middle.</summary>
    private static void AddRecord(ToolEdit edit, RockGrid grid, (int X, int Z) column, List<DigOp> ops)
    {
        float[] seeds = grid.SeedsOf(column.X, column.Z) ?? grid.GroundSeeds(column.X, column.Z);
        byte[] bytes = new ColumnRecord(ColumnRecord.Plain, seeds, 0, ops).Encode();
        float half = RockGrid.ColumnMetres / 2;
        float x = (column.X * RockGrid.ColumnMetres) + half, z = (column.Z * RockGrid.ColumnMetres) + half;
        // The mod puts its record at the column's middle, on the ground it was dug from.
        float y = grid.SeedAt((column.X * RockGrid.Samples) + (RockGrid.Samples / 2), (column.Z * RockGrid.Samples) + (RockGrid.Samples / 2));
        edit.Add(TunnelheimMod.RecordPrefabName, new Vector3(x, y, z), Vector3.Zero, c => c.SetBytes(TunnelheimMod.RecordKeyName, bytes));
    }

    /// <summary>Each column's first record the mod reads, with its object: the one new edits go to.</summary>
    private static Dictionary<(int X, int Z), (IObjectView Object, ColumnRecord Record)> Records(IAreaView area, Action<string> log)
    {
        var records = new Dictionary<(int X, int Z), (IObjectView, ColumnRecord)>();
        foreach (var o in area.ObjectsOf(TunnelheimMod.RecordPrefabName))
        {
            var column = RockGrid.ColumnAt(o.Position.X, o.Position.Z);
            if (!records.ContainsKey(column) && o.TryGetBytes(TunnelheimMod.RecordKeyName, out byte[] bytes) && Readable(o, bytes, log) is { } record)
            {
                records[column] = (o, record);
            }
        }
        return records;
    }

    private static ColumnRecord? Readable(IObjectView o, byte[] bytes, Action<string> log)
    {
        try
        {
            return ColumnRecord.Parse(bytes);
        }
        catch (InvalidDataException e)
        {
            log($"{o.Prefab} at ({o.Position.X:0.#}, {o.Position.Z:0.#}) kept as it is, new edits go to a record of their own: {e.Message}");
            return null;
        }
    }
}
