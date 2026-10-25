using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>
    /// Writes everything into the world's files, like Apply live: the world stays open as it is, with
    /// its history (an undo, saved again, is saved). Each save is made from the save the world was opened
    /// from, with every change since (so a zone saved before and not changed since is written again),
    /// and the previous save of the session goes. No limit ground goes into the files as ground discs and
    /// stays No limit ground in the session, so its history stays exact (an undo, saved again, takes the discs
    /// away). After zone resets, the world is read again from what was written (the zones are not the same),
    /// and the history goes across it (<see cref="TakeBackHistory"/>).
    /// A save the game made after the world was read (a play test between two saves) is never thrown
    /// away: the save is refused before anything changes, No limit ground included.
    /// </summary>
    public Outcome Save()
    {
        int known = SaveNumber;
        if (WorldWriter.Newer(World.Directory, known) is Said newer)
        {
            return new Outcome(false, newer, false, new WorldWriter.Result(false, newer.Shown, null, 0, 0, []) { Report = newer });
        }
        // Zone resets: the world is read again after the save, from which the history's objects cannot be read; it is
        // taken now (No limit ground going back to the session after the save needs none, see below).
        var resets = Edits.Resets;
        var carried = resets.Count > 0 ? CarryHistory() : null;
        var lift = LiftToDiscs();
        // The changed zones and those the last save wrote (each copied once).
        var changed = Edits.Copies(e => e.Changed || _savedZones.Contains((e.ZoneX, e.ZoneZ)));
        var zones = changed.Where(e => e.Changed).Select(e => (e.ZoneX, e.ZoneZ)).ToHashSet();
        zones.UnionWith(_savedZones);
        bool reread = resets.Count > 0;
        WorldWriter.Result result;
        try
        {
            // Saved already and objects differ from that save (placed then undone, deleted then put back): written even
            // when nothing differs from the save opened, which the last one then no longer matches.
            var (_, del, add, _) = Pending;
            bool rewrite = _lastSave != null && del + add > 0;
            result = WorldWriter.Save(World, changed, Edits.Deleted, Edits.Added, Edits.Resets, new WorldWriter.Options(KeepBase: !reread, Drop: _lastSave, Latest: known, Rewrite: rewrite));
        }
        catch
        {
            if (lift != null)
            {
                Unlift(lift);
            }
            throw;
        }
        // Not saved: the No limit ground stays as it was (the area and its history too), unless the edits changed
        // meanwhile: then it stays as discs, and the area is read again.
        if (!result.Saved && lift != null && Unlift(lift))
        {
            lift = null;
        }
        // Saved: the ground discs are in the files only, and the session goes back to its No limit ground (the trees
        // and rocks the discs took away stand in it too): the next save is made from the save the world was opened
        // from, which has none of them, and works them out again. Edits changed meanwhile keep the discs, and the
        // world is read again.
        bool unlifted = result.Saved && !reread && lift != null && Unlift(lift);
        reread |= result.Saved && lift != null && !unlifted;
        bool lifted = lift != null && !unlifted;
        // The discs stay: the history is taken now, from the ground as it was before them (the world is not read yet).
        if (lifted && carried == null)
        {
            carried = CarryHistory(lift);
        }
        Said unread = Said.Empty;
        if (result.Saved && reread)
        {
            unread = ReadBackSave(result);
            if (lift != null)
            {
                KeepLiftForm(lift, resets, read: true);
            }
        }
        else if (result.Saved)
        {
            // What the files hold where the discs are, which the history file keeps the No limit ground of: the zones
            // just worked out, and those still as the save the world was read from holds them.
            _discZones = _discZones.Where(z => Edits.AsPlanned(z.Key.X, z.Key.Z)).ToDictionary();
            foreach (var z in lift?.Plan.Zones ?? [])
            {
                _discZones[(z.ZoneX, z.ZoneZ)] = HistoryFile.Written(z);
            }
            _lastSave = result.Files;
            _lastNumber = result.Number;
            _savedZones = zones;
            Edits.MarkApplied(changed.Select(e => (e.ZoneX, e.ZoneZ)));
            _savedDeleted = [.. Edits.Deleted];
            _savedAdded = [.. Edits.Added];
            MarkHistoryApplied();
            KeepHistory();
        }
        // Read again (the world, or the ground under the area with the discs in it): the history comes along.
        if ((result.Saved && reread) || lifted)
        {
            TakeBackHistory(carried, result.Saved ? resets : [], done: result.Saved);
        }
        var message = result.Report + (lift != null ? Said.Of("save.msg.lifted", lift.Plan.Describe()) : Said.Empty) + unread;
        return new Outcome(result.Saved, message, result.Saved && reread, result with { Message = message.Shown, Report = message }) { Lifted = lifted };
    }
}
