using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>
    /// Offline, after the game saved the world since it was read (<see cref="GameSaveSince"/>): reads the game's save and
    /// works out how the pending changes go onto it, and what the game changed too (<see cref="EditReplay.Clashes"/>).
    /// Nothing changes yet; <see cref="Replay"/> does it. Null when the game made no newer save.
    /// </summary>
    public EditReplay? PrepareReplay() => GameSaveSince() == null
        ? null
        : EditReplay.Prepare(World, Edits, new EditReplay.Saved(_savedZones, _savedDeleted, _savedAdded), WorldSave.Load(World.Directory));

    /// <summary>
    /// Brings the pending changes onto the game's save (<see cref="PrepareReplay"/>): the world is that save from now
    /// on, with every change pending on it, the clashes settled as <paramref name="keepMine"/> says per kind. Nothing
    /// is written to disk: the next save writes it, from the game's save. The history comes along, fitted to the
    /// game's save, its steps asked about before they are undone (the world changed under them); steps that touch what
    /// the game's version was kept for, and the steps before them, are dropped. The area open must be read again
    /// (Reloaded): its history was taken here.
    /// </summary>
    /// <param name="plan">What <see cref="PrepareReplay"/> worked out.</param>
    /// <param name="keepMine">Per kind of clash: true keeps the editor's change, false the game's version.</param>
    public Outcome Replay(EditReplay plan, Func<ReplayClash, bool> keepMine)
    {
        // The game saved again since its save was read: that save is not the latest any more.
        if (WorldSave.CommittedSave(World.Directory) != plan.Newer.SaveNumber)
        {
            return new Outcome(false, Said.Of("replay.savedAgain"), false);
        }
        var kept = Area?.Export() ?? History;
        var r = plan.Apply(keepMine, Edits, NextId);
        if (r == null)
        {
            return new Outcome(false, Said.Of("replay.stale"), false);
        }
        World = plan.Newer;
        UseModifiers(new TerrainModifiers(Uplift.PlacedNow(World, Edits)));
        // The game's save is the base now; the files of the session's own save may be part of it, so they stay.
        ForgetSaves();
        Area = null;
        var (history, steps, dropped) = Refit(kept, plan, r);
        History = history;
        Restored = history == null ? null : new HistoryFile.Restored(history, steps, true, DateTime.Now) { Replayed = true };
        EarlierAllowed = false;
        RestoredSaid = true;
        var text = Said.Of("replay.done", World.SaveNumber, r.Zones, r.Deleted, r.Added, r.Resets);
        if (r.GameGround.Count > 0 || r.Dropped.Count > 0)
        {
            text += Said.Of("replay.gameKept", r.GameGround.Count, plan.Clashes.Count(c => c.Kind != ReplayClash.Ground && !keepMine(c.Kind)));
        }
        if (r.Unread > 0)
        {
            text += Said.Of("replay.unread", r.Unread);
        }
        if (steps + dropped > 0)
        {
            text += dropped > 0 ? Said.Of("replay.historyDropped", steps, dropped) : Said.Of("replay.history", steps);
        }
        return new Outcome(true, text, true);
    }
}
