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
    /// <param name="history">The history to keep; null: the open area's, or the world's.</param>
    private void KeepHistory(bool later = false, EditSession.Kept? history = null)
    {
        try
        {
            if ((history ?? Area?.Export() ?? History) is { } kept)
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
            MarkApplied(History, sent);
        }
    }

    /// <summary>A history's steps saved or applied: the undo steps the world has, and the undone ones not.</summary>
    /// <param name="k">The history.</param>
    /// <param name="sent">After a partial Apply live, whether the game has all a step did (null: every step).</param>
    private static void MarkApplied(EditSession.Kept k, Func<EditSession.Change, bool>? sent)
    {
        EditSession.MarkSent(k.Undo, sent ?? (static _ => true));
        // The undone steps are not in the world now (see EditSession.MarkApplied).
        if (sent == null)
        {
            foreach (var c in k.Redo)
            {
                c.Applied = false;
            }
        }
    }

    /// <summary>The history taken before the world is read again, with the earlier session it may hold steps of.</summary>
    /// <param name="Data">The history as <see cref="HistoryFile.Carry"/> took it.</param>
    /// <param name="Earlier">The earlier session's history the world was opened with, if any (<see cref="Restored"/>).</param>
    private sealed record CarriedHistory(byte[] Data, HistoryFile.Restored? Earlier);

    /// <summary>
    /// The history as it is now, taken before the world, or the ground under the area, is read again
    /// (<see cref="TakeBackHistory"/> fits it to what was read); null when there is no step.
    /// </summary>
    private CarriedHistory? CarryHistory() => HistoryFile.Carry(this, Area?.Export() ?? History) is { } data ? new(data, Restored) : null;

    /// <summary>
    /// After the world, or the ground under the area, was read again (zone resets, No limit ground made into ground
    /// discs): the history carried across it (<see cref="HistoryFile.Carry"/>, taken before) fitted to it, so its steps
    /// can still be undone: objects found again, ground values onto the generated ground as it is now, what they did in
    /// the zones reset left out. The open area takes it as it is read again (EditSession.Reread).
    /// </summary>
    /// <param name="carried">The history as carried (<see cref="CarryHistory"/>); null: there was none.</param>
    /// <param name="reset">The zones reset meanwhile.</param>
    /// <param name="done">Saved or applied: the steps are marked so (as <paramref name="sent"/> says) and kept on disk.</param>
    /// <param name="sent">After a partial Apply live, whether the game has all a step did (null: every step).</param>
    /// <param name="later">Apply live: the history file is written in the background.</param>
    private void TakeBackHistory(CarriedHistory? carried, IReadOnlyCollection<Editing.ZoneReset> reset, bool done, Func<EditSession.Change, bool>? sent = null, bool later = false)
    {
        var back = carried == null ? null : HistoryFile.TakeBack(this, carried.Data, reset);
        History = back?.Kept;
        // The earlier session's steps among them are still asked about as before.
        Restored = carried?.Earlier is { } earlier && back != null ? earlier with { Kept = back.Kept } : null;
        if (!done)
        {
            return;
        }
        if (History is { } k)
        {
            MarkApplied(k, sent);
            KeepHistory(later, k);
        }
        else
        {
            HistoryFile.Delete(this);
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
