using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>
    /// Writes everything into the world's files, like Apply live: the world stays open as it is, with
    /// its history (an undo, saved again, is saved). Each save is made from the save the world was opened
    /// from, with every change since (so a zone saved before and not changed since is written again),
    /// and the previous save of the session goes. After zone resets or when No limit ground became ground
    /// discs, the world is read again from what was written (the ground or zones are not the same).
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
        var lift = LiftToDiscs();
        // The changed zones and those the last save wrote (each copied once).
        var changed = Edits.Copies(e => e.Changed || _savedZones.Contains((e.ZoneX, e.ZoneZ)));
        var zones = changed.Where(e => e.Changed).Select(e => (e.ZoneX, e.ZoneZ)).ToHashSet();
        zones.UnionWith(_savedZones);
        bool reread = lift != null || Edits.ResetCount > 0;
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
        Said unread = Said.Empty;
        if (result.Saved && reread)
        {
            unread = ReadBackSave(result);
        }
        else if (result.Saved)
        {
            _lastSave = result.Files;
            _lastNumber = result.Number;
            _savedZones = zones;
            Edits.MarkApplied(changed.Select(e => (e.ZoneX, e.ZoneZ)));
            _savedDeleted = [.. Edits.Deleted];
            _savedAdded = [.. Edits.Added];
            MarkHistoryApplied();
            KeepHistory();
        }
        var message = result.Report + (lift != null ? Said.Of("save.msg.lifted", lift.Plan.Describe()) : Said.Empty) + unread;
        return new Outcome(result.Saved, message, result.Saved && reread, result with { Message = message.Shown, Report = message }) { Lifted = lift != null };
    }
}
