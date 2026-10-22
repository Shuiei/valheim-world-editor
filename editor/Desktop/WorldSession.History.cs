using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>
    /// After a save or Apply live: the history as it is now (it matches what the world holds) goes to
    /// disk. A failure to write it is only logged: the save itself is done.
    /// </summary>
    /// <param name="later">Apply live: taken now, written in the background (<see cref="HistoryFile.WriteLater"/>),
    /// since it can come at every edit.</param>
    private void KeepHistory(bool later = false)
    {
        try
        {
            if ((Area?.Export() ?? History) is { } kept)
            {
                if (later)
                {
                    HistoryFile.WriteLater(this, kept);
                }
                else
                {
                    HistoryFile.Write(this, kept);
                }
            }
        }
        catch (Exception ex)
        {
            // The save itself is done: any failure here is logged (an unexpected one in full), not reported as a
            // failed save.
            Log.Caught(ex, "keeping the history on disk (the save itself is done)");
        }
    }

    /// <summary>
    /// Saved or applied from the map (no area open): the kept steps are in the world now, as an open
    /// area's are once it saves (EditSession), so a later Discard does not undo them.
    /// </summary>
    /// <param name="sent">After a partial Apply live, whether the game has all a step did (null: every step).</param>
    private void MarkHistoryApplied(Func<EditSession.Change, bool>? sent = null)
    {
        if (Area == null && History != null)
        {
            EditSession.MarkSent(History.Undo, sent ?? (static _ => true));
            // The undone steps are not in the world now (see EditSession.MarkApplied).
            if (sent == null)
            {
                foreach (var c in History.Redo)
                {
                    c.Applied = false;
                }
            }
        }
    }

    /// <summary>
    /// What is waiting to be saved (live: to be applied to the game): changed zones, deletions, added objects,
    /// zone resets. Once saved offline, the objects count what differs from that save.
    /// </summary>
    public (int Zones, int Deleted, int Added, int Resets) Pending
    {
        get
        {
            if (IsLive)
            {
                var (d, a) = LiveSync.Pending(Edits);
                return (Edits.ChangedZoneCount, d, a, Edits.ResetCount);
            }
            if (_lastSave == null)
            {
                return (Edits.ChangedZoneCount, Edits.DeletedCount, Edits.AddedCount, Edits.ResetCount);
            }
            // Saved already: what differs from that save (an undo after it is pending too), counted by what the next
            // save does to the world: an object placed and saved, then undone, is taken out (deleted); one deleted
            // and saved, then undone, is put back (added).
            // Counted against the saved sets only (no set of the current ones): after a save with millions of
            // new objects this runs at every change. Both lists hold each object once.
            var (del, add) = Edits.WithObjects((deleted, added) =>
            {
                int keptDel = deleted.Count(_savedDeleted.Contains), keptAdd = added.Values.Count(_savedAdded.Contains);
                return (deleted.Count - keptDel + _savedAdded.Count - keptAdd, added.Count - keptAdd + _savedDeleted.Count - keptDel);
            });
            return (Edits.ChangedZoneCount, del, add, Edits.ResetCount);
        }
    }
}
