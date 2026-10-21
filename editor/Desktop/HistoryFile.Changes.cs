using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public static partial class HistoryFile
{
    /// <summary>
    /// Reads the undo and redo steps of a history file, in the order they were kept.
    /// </summary>
    /// <param name="i">The file, positioned at the first step.</param>
    /// <param name="undoCount">How many of the steps are undo steps; the redo steps follow them.</param>
    /// <param name="redoCount">How many redo steps there are.</param>
    /// <param name="ids">The id each of the file's objects was given, by its index in the file (a step's things refer to them).</param>
    /// <param name="version">The file's format (1: no hidden objects).</param>
    /// <returns>Each step's change with the index of the step it reverts (-1 for none), its points (not yet fitted to a grid), and the ids of its things.</returns>
    internal static (List<(EditSession.Change C, int RevertOf)> Changes, List<(int X, int Z)[]> Points, List<int[]> ThingIds) ReadChanges(BinaryReader i, int undoCount, int redoCount, List<int> ids, int version = Version)
    {
        var changes = new List<(EditSession.Change C, int RevertOf)>();
        var points = new List<(int X, int Z)[]>();
        var thingIds = new List<int[]>();

        for (int n = 0; n < undoCount + redoCount; n++)
        {
            string label = i.ReadString();
            var time = new DateTime(i.ReadInt64(), DateTimeKind.Local);
            bool removed = i.ReadBoolean(), applied = i.ReadBoolean();
            int revertOf = i.ReadInt32();
            int np = ReadCount(i);
            var pts = new List<(int X, int Z)>(Capacity(np));
            for (int k = 0; k < np; k++)
            {
                pts.Add((i.ReadInt32(), i.ReadInt32()));
            }
            var before = ReadState(i, np);
            var after = ReadState(i, np);
            int nz = ReadCount(i);
            var zones = new List<(int X, int Z)>(Capacity(nz));
            for (int k = 0; k < nz; k++)
            {
                zones.Add((i.ReadInt32(), i.ReadInt32()));
            }
            int nt = ReadCount(i);
            var things = new List<(int Index, bool Before, bool After)>(Capacity(nt));
            var tids = new List<int>(Capacity(nt));
            for (int k = 0; k < nt; k++)
            {
                int t = i.ReadInt32();
                tids.Add(ids[t]);
                things.Add((k, i.ReadBoolean(), i.ReadBoolean()));
            }
            int nr = ReadCount(i);
            var resets = new List<(ZoneReset, bool, bool)>(Capacity(nr));
            for (int k = 0; k < nr; k++)
            {
                resets.Add((new ZoneReset(i.ReadInt32(), i.ReadInt32(), i.ReadBoolean(), i.ReadBoolean()), i.ReadBoolean(), i.ReadBoolean()));
            }
            // Version 2: the hidden objects (mods' data holders), by object.
            var hidden = new List<(int Id, bool Before, bool After)>();
            int nh = version >= 2 ? ReadCount(i) : 0;
            for (int k = 0; k < nh; k++)
            {
                hidden.Add((ids[i.ReadInt32()], i.ReadBoolean(), i.ReadBoolean()));
            }
            // Points are fitted below, once the grid holding all of them is known.
            var c = new EditSession.Change(label, [], before, after, zones)
            {
                Things = [.. things],
                Hidden = [.. hidden],
                Resets = [.. resets],
                Time = time,
                Removed = removed,
                Applied = applied || n < undoCount,
                Earlier = true,
            };
            changes.Add((c, revertOf));
            points.Add([.. pts]);
            thingIds.Add([.. tids]);
        }
        return (changes, points, thingIds);
    }
}
