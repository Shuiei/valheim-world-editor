using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Zone resets: the entries a step puts on the history, and marking and unmarking the zones.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// A step's reset entries for one zone going from one marking to another (null: not marked). A zone marked with
    /// other options gets two: the old marking taken off and the new one put on, so undo and redo bring back each
    /// side's options (SetResets takes marks off first).
    /// </summary>
    /// <param name="before">The zone's marking before the step.</param>
    /// <param name="after">Its marking after the step.</param>
    private static IEnumerable<(ZoneReset Reset, bool Before, bool After)> ResetEntries(ZoneReset? before, ZoneReset? after)
    {
        if (before == after)
        {
            if (before != null)
            {
                yield return (before, true, true);
            }
            yield break;
        }
        if (before != null)
        {
            yield return (before, true, false);
        }
        if (after != null)
        {
            yield return (after, false, true);
        }
    }

    /// <summary>
    /// Marks and unmarks zones for reset as one side of a step's entries says: every mark is taken off before any is
    /// put on, so a zone whose options the step changed ends with that side's options.
    /// </summary>
    /// <param name="resets">The step's entries.</param>
    /// <param name="after">The side to set: after the step (do, redo) or before it (undo).</param>
    private void SetResets(IReadOnlyCollection<(ZoneReset Reset, bool Before, bool After)> resets, bool after)
    {
        foreach (var (r, b, a) in resets)
        {
            if (!(after ? a : b))
            {
                Edits.SetReset(r, false);
            }
        }
        foreach (var (r, b, a) in resets)
        {
            if (after ? a : b)
            {
                Edits.SetReset(r, true);
            }
        }
    }

    /// <summary>Unmarks those of these zones that are marked for reset, as one undo step.</summary>
    /// <returns>How many were unmarked (none: no step).</returns>
    public int UnmarkResets(IEnumerable<(int X, int Z)> zones, string label)
    {
        var marked = Resets.ToDictionary(r => (r.X, r.Z));
        var found = zones.Where(marked.ContainsKey).Distinct().ToList();
        if (found.Count > 0)
        {
            Commit(label, null, [], [], [.. found.Select(z => (marked[z], true, false))]);
        }
        return found.Count;
    }

    /// <summary>
    /// The zone reset entries a removal puts back: a zone goes back only while it is still marked as the step left it
    /// (as paint); a later step's marking stays.
    /// </summary>
    /// <param name="c">The change taken out.</param>
    private (ZoneReset Reset, bool Before, bool After)[] ResetsToRevert(Change c)
    {
        var marked = Edits.Resets.ToDictionary(r => (r.X, r.Z));
        return [.. c.Resets.GroupBy(r => (r.Reset.X, r.Reset.Z))
            .SelectMany(z =>
            {
                ZoneReset? now = marked.GetValueOrDefault(z.Key);
                ZoneReset? left = z.Where(r => r.After).Select(r => r.Reset).FirstOrDefault();
                return ResetEntries(now, now == left ? z.Where(r => r.Before).Select(r => r.Reset).FirstOrDefault() : now);
            })];
    }
}
