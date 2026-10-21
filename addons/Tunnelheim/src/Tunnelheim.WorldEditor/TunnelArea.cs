using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// The tunnels of an area as the game builds them when it loads them: its records and checkpoints read and replayed
/// over the ground the game shows there, with the game's hoe strokes and the Deep North as the player's Valheim has them.
/// </summary>
public static class TunnelArea
{
    /// <summary>The rock of an area: every column with a record built (none when it has no records).</summary>
    /// <param name="area">The area, its objects as they are with the edits not saved yet.</param>
    /// <param name="hoeOps">The game's terrain operations' settings (<see cref="TerrainOps"/>).</param>
    /// <param name="log">Where a record or checkpoint that cannot be read is told (the column is then built as the game would).</param>
    public static RockGrid Build(IAreaView area, IReadOnlyDictionary<int, HoeSettings> hoeOps, Action<string> log)
    {
        var grid = new RockGrid(area.GroundHeight, new RockRules(RockRules.DefaultMaxBuildHeight, hoeOps, area.IsDeepNorth));
        grid.Build(Columns(area, log));
        return grid;
    }

    /// <summary>Each column's readable records (in the order the area gives them) and checkpoints by id.</summary>
    /// <param name="area">The area.</param>
    /// <param name="log">Where what cannot be read is told.</param>
    public static List<ColumnRecords> Columns(IAreaView area, Action<string> log)
    {
        var records = new Dictionary<(int X, int Z), List<ColumnRecord>>();
        foreach (var o in area.ObjectsOf(TunnelheimMod.RecordPrefabName))
        {
            if (o.TryGetBytes(TunnelheimMod.RecordKeyName, out byte[] bytes) && Read(o, bytes, ColumnRecord.Parse, log) is { } record)
            {
                var at = RockGrid.ColumnAt(o.Position.X, o.Position.Z);
                (records.TryGetValue(at, out var list) ? list : records[at] = []).Add(record);
            }
        }
        var checkpoints = new Dictionary<(int X, int Z), Dictionary<int, ColumnCheckpoint>>();
        foreach (var o in area.ObjectsOf(TunnelheimMod.CheckpointPrefabName))
        {
            if (o.TryGetInt(TunnelheimMod.CheckpointIdKeyName, out int id) && o.TryGetBytes(TunnelheimMod.CheckpointKeyName, out byte[] bytes)
                && Read(o, bytes, ColumnCheckpoint.Parse, log) is { } checkpoint)
            {
                var at = RockGrid.ColumnAt(o.Position.X, o.Position.Z);
                (checkpoints.TryGetValue(at, out var byId) ? byId : checkpoints[at] = [])[id] = checkpoint;
            }
        }
        return [.. records.Select(r => new ColumnRecords(r.Key.X, r.Key.Z, r.Value,
            checkpoints.TryGetValue(r.Key, out var byId) ? byId : []))];
    }

    /// <summary>An object's record or checkpoint; null (told) when its bytes are not one the mod reads.</summary>
    private static T? Read<T>(IObjectView o, byte[] bytes, Func<byte[], T> parse, Action<string> log)
        where T : class
    {
        try
        {
            return parse(bytes);
        }
        catch (InvalidDataException e)
        {
            log($"{o.Prefab} at ({o.Position.X:0.#}, {o.Position.Z:0.#}) left out: {e.Message}");
            return null;
        }
    }
}
