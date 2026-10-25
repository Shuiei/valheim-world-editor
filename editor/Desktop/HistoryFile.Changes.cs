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
    /// <param name="ids">
    /// The id each of the file's objects was given, by its index in the file (a step's things refer to them);
    /// <see cref="Dropped"/> for one left out (it was in a zone reset since).
    /// </param>
    /// <param name="version">The file's format (1: no hidden objects; before 3: no generated ground, all from an earlier session).</param>
    /// <param name="fit">How the steps are fitted to the world as it is now; null: as written.</param>
    /// <returns>Each step's change with the index of the step it reverts (-1 for none), its points (not yet fitted to a grid), and the ids of its things.</returns>
    internal static (List<(EditSession.Change C, int RevertOf)> Changes, List<(int X, int Z)[]> Points, List<int[]> ThingIds) ReadChanges(BinaryReader i, int undoCount, int redoCount, List<int> ids, int version = Version, Fitting? fit = null)
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
            // Version 3: whether the step came from an earlier session (before, every step of a file did).
            bool earlier = version < 3 || i.ReadBoolean();
            int np = ReadCount(i);
            var pts = new List<(int X, int Z)>(Capacity(np));
            for (int k = 0; k < np; k++)
            {
                pts.Add((i.ReadInt32(), i.ReadInt32()));
            }
            // Version 3: the generated ground the values are on.
            float[]? bases = version >= 3 ? ReadFloats(i, np) : null;
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
                int id = ids[i.ReadInt32()];
                bool b = i.ReadBoolean(), a = i.ReadBoolean();
                if (id != Dropped)
                {
                    things.Add((tids.Count, b, a));
                    tids.Add(id);
                }
            }
            int nr = ReadCount(i);
            var resets = new List<(ZoneReset, bool, bool)>(Capacity(nr));
            for (int k = 0; k < nr; k++)
            {
                var reset = new ZoneReset(i.ReadInt32(), i.ReadInt32(), i.ReadBoolean(), i.ReadBoolean());
                bool b = i.ReadBoolean(), a = i.ReadBoolean();
                // A reset done since cannot be taken back.
                if (fit == null || !fit.Reset.ContainsKey((reset.X, reset.Z)))
                {
                    resets.Add((reset, b, a));
                }
            }
            // Version 2: the hidden objects (mods' data holders), by object.
            var hidden = new List<(int Id, bool Before, bool After)>();
            int nh = version >= 2 ? ReadCount(i) : 0;
            for (int k = 0; k < nh; k++)
            {
                int id = ids[i.ReadInt32()];
                bool b = i.ReadBoolean(), a = i.ReadBoolean();
                if (id != Dropped)
                {
                    hidden.Add((id, b, a));
                }
            }
            if (fit != null)
            {
                if (bases != null)
                {
                    fit.Rebase(pts, bases, before, after);
                }
                (pts, before, after, zones) = fit.LeaveOutReset(pts, before, after, zones);
            }
            // Points are fitted below, once the grid holding all of them is known.
            var c = new EditSession.Change(label, [], before, after, zones)
            {
                Things = [.. things],
                Hidden = [.. hidden],
                Resets = [.. resets],
                Time = time,
                Removed = removed,
                // Carried across a re-read, a step keeps its flags; read from a file, the undo steps are in the world
                // and every step is from an earlier session.
                Applied = applied || (fit is not { Carried: true } && n < undoCount),
                Earlier = earlier || fit is not { Carried: true },
            };
            changes.Add((c, revertOf));
            points.Add([.. pts]);
            thingIds.Add([.. tids]);
        }
        return (changes, points, thingIds);
    }
}
