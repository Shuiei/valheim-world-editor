using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>One Apply live, with the lock held; a failure of the game's answer comes back as the outcome.</summary>
    private async Task<Outcome> ApplyLiveOnce()
    {
        var live = Live!;
        var done = new List<Said>();
        bool reloaded = false;
        var lift = LiftToDiscs();
        var plan = lift?.Plan;
        if (plan != null)
        {
            done.Add(Said.Of("save.done.lifted", plan.Describe()));
        }
        // Until the ground or objects went in (sent), or the ground may have (unsure), a failure has sent nothing: the
        // No limit ground is put back as it was.
        bool sent = false, unsure = false, objectsDone = false;
        try
        {
            // Copies as sent: edits made while the game answers are not marked as applied.
            var zones = Edits.Copies(e => e.Changed);
            if (zones.Count > 0)
            {
                try
                {
                    await live.ApplyTerrain(zones.Select(e => (e.ZoneX, e.ZoneZ, WorldWriter.EncodeTerrain(e))).ToList());
                }
                catch (Exception ex) when (!NothingSent(ex))
                {
                    // No answer, or one after the game may have taken the ground: the discs stay (the area is read
                    // again), and the next Apply live sends the ground again, which only sets each zone's ground anew.
                    unsure = true;
                    throw;
                }
                sent = true;
                Edits.MarkApplied(zones);
                done.Add(Said.Of("save.count.zones", zones.Count));
            }
            var objects = await LiveSync.Apply(World, Edits, live);
            objectsDone = true;
            if (objects != Said.Empty)
            {
                sent = true;
                done.Add(objects);
            }
            if (Edits.ResetCount > 0)
            {
                var resets = Edits.Resets;
                await live.ResetZones(resets);
                // Done in the game: not pending any more, even if reading the world below fails (sent again, they
                // would wipe what players built there since).
                Edits.MarkResetsApplied(resets);
                done.Add(Said.Of("save.done.resets", resets.Count));
                try
                {
                    World = await live.LoadWorld();
                }
                catch (Exception ex) when (GameFailed(ex))
                {
                    Log.Caught(ex, "reading the world from the game after resetting zones");
                    MarkHistoryApplied();
                    KeepHistory(later: true);
                    return new Outcome(true, Said.Of("save.msg.appliedUnread", Said.Join("; ", done), ex.Message), false) { Lifted = plan != null };
                }
                Edits.ResetFrom(World);
                UseModifiers(new TerrainModifiers(World));
                LiveSync.Reset();
                _digests.Clear();
                _nextId = -1;
                ForgetHistory();
                reloaded = true;
            }
            else if (done.Count > 0)
            {
                MarkHistoryApplied();
                KeepHistory(later: true);
            }
            return new Outcome(done.Count > 0, done.Count > 0 ? Said.Of("save.msg.applied", Said.Join("; ", done)) : Said.Of("save.msg.nothingToApply"), reloaded) { Lifted = plan != null };
        }
        // The game answering with an error (InvalidOperationException from the bridge) is reported too.
        catch (Exception ex) when (GameFailed(ex))
        {
            Log.Caught(ex, "applying the changes to the running game");
            if (sent)
            {
                // Some parts went in: said, and the steps the game has all of are applied.
                var resets = Edits.Resets.Select(r => (r.X, r.Z)).ToHashSet();
                var partly = new Outcome(true, Said.Of("save.msg.appliedPartly", Said.Join("; ", done), ex.Message), false)
                {
                    Lifted = lift != null,
                    // Resets only while some are still to send: a reset unmarked since leaves none, though steps keep it.
                    Unsent = (objectsDone ? LiveParts.None : LiveParts.Objects) | (resets.Count > 0 ? LiveParts.Resets : LiveParts.None),
                    UnsentResets = resets,
                };
                MarkHistoryApplied(partly.Sent);
                KeepHistory(later: true);
                return partly;
            }
            bool lifted = lift != null && (unsure || !Unlift(lift));
            return new Outcome(false, Said.Of("save.msg.couldNotApply", ex.Message), false) { Lifted = lifted };
        }
    }
}
