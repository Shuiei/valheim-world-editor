using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// Tunnels going with the editor's copies (the area copier hook): the edits whose middle lies in what is copied, from
/// every record of the area, each once and in the order the game plays them, kept as offsets from the copy's middle;
/// pasted, they are moved, turned and mirrored with each copy and saved as the Tunnel tool saves its own
/// (<see cref="TunnelEdits"/>), into the records already there. Edits the mod has baked into a column's checkpoint are
/// no longer edits and are not copied (the log says so); ore vein marks are the game's to make.
/// </summary>
/// <param name="host">The editor, for the game's hoe strokes.</param>
/// <param name="log">Where what is left out is told.</param>
public sealed class TunnelCopies(IEditorHost host, Action<string> log) : IAreaCopier
{
    /// <inheritdoc/>
    public byte[]? Copy(IAreaView area, CopyRegion region)
    {
        var ops = new List<DigOp>();
        int baked = 0;
        foreach (var column in TunnelArea.Columns(area, log))
        {
            foreach (var record in column.Records)
            {
                baked += record.CheckpointId != 0 && Overlaps(region, column.X, column.Z) ? 1 : 0;
                Merge(ops, [.. record.Ops.Where(o => o.Kind != DigKind.Vein && Inside(region, o))]);
            }
        }
        if (baked > 0)
        {
            log($"copy: {baked} column record(s) there start from a checkpoint; what was baked into it is not copied");
        }
        if (ops.Count == 0)
        {
            return null;
        }
        var middle = region.Middle;
        return new ColumnRecord(ColumnRecord.Plain, null, 0, [.. ops.Select(o => o with { Position = o.Position - middle })]).Encode();
    }

    /// <inheritdoc/>
    public ToolEdit? Paste(IAreaView area, byte[] kept, IReadOnlyList<CopyPlacement> copies)
    {
        ColumnRecord saved;
        try
        {
            saved = ColumnRecord.Parse(kept);
        }
        catch (InvalidDataException e)
        {
            log("paste: the copy's tunnels are not in a form this add-on reads, left out: " + e.Message);
            return null;
        }
        var ops = copies.SelectMany(c => saved.Ops.Select(o => DigOps.Moved(o, c))).ToList();
        var grid = TunnelArea.Build(area, TerrainOps.From(host.Game.Components(TerrainOps.ScriptClass)), log);
        return TunnelEdits.Make(area, grid, ops, log).Edit;
    }

    /// <summary>Whether an edit's middle lies in what is copied.</summary>
    private static bool Inside(CopyRegion region, in DigOp op)
    {
        var centre = op.Carves ? DigShapes.Bounds(op).Centre : op.Position;
        return region.Contains(centre.X, centre.Z);
    }

    /// <summary>Whether a column has a corner or its middle in what is copied.</summary>
    private static bool Overlaps(CopyRegion region, int cx, int cz)
    {
        float x0 = cx * RockGrid.ColumnMetres, z0 = cz * RockGrid.ColumnMetres, x1 = x0 + RockGrid.ColumnMetres, z1 = z0 + RockGrid.ColumnMetres;
        return region.Contains(x0, z0) || region.Contains(x1, z0) || region.Contains(x0, z1) || region.Contains(x1, z1)
            || region.Contains((x0 + x1) / 2, (z0 + z1) / 2);
    }

    /// <summary>
    /// Adds a record's edits to those gathered so far, each once (an edit across columns is kept by each column's
    /// record): one already there marks where the record's next new edits go, after it, so the order the game played
    /// them in is kept.
    /// </summary>
    private static void Merge(List<DigOp> ops, List<DigOp> record)
    {
        int at = -1;
        foreach (var op in record)
        {
            int found = ops.IndexOf(op);
            if (found >= 0)
            {
                at = found;
                continue;
            }
            ops.Insert(++at, op);
        }
    }
}
