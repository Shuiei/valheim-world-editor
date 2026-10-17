using TerrainEditor.App;

using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Places the last level's antechamber, arena and treasury at the way's end. The antechamber is kept only with
    /// the arena beyond it: one with no arena would be a dead end.
    /// </summary>
    /// <param name="l">The last level.</param>
    /// <param name="cur">The way's last room.</param>
    private void PlaceArena(DungeonPlan.Level l, DungeonPlan.Space cur)
    {
        var before = Mark(l);
        var ante = Attach(l, cur, Room.Antechamber, main: true) ?? Attach(l, AnyParent(l, main: true), Room.Antechamber, main: true);
        var boss = ante == null ? null : Attach(l, ante, Room.Boss, main: true, forward: true) ?? Attach(l, ante, Room.Boss, main: true);
        if (boss == null)
        {
            Drop(l, before);
            boss = AttachArenaAnywhere(l, before);
        }
        if (boss != null)
        {
            _plan.Boss = boss;
            _plan.Treasury = Attach(l, boss, Room.Treasury, main: true, forward: true) ?? Attach(l, boss, Room.Treasury, main: true);
        }
        else
        {
            _plan.Notes.Add(Said.Of("dungeon.msg.noArena"));
        }
    }

    /// <summary>
    /// Not enough room ahead of the way: tries an antechamber and arena off each room that can lead on, furthest from the
    /// entrance first, taking back each attempt that does not fit.
    /// </summary>
    /// <param name="l">The last level.</param>
    /// <param name="before">The level's plan before the way was laid, to take back to.</param>
    /// <returns>The arena that fits, or null when none does.</returns>
    private DungeonPlan.Space? AttachArenaAnywhere(DungeonPlan.Level l, Point before)
    {
        foreach (var p in l.Spaces.Where(s => s.IsRoom && CanParent(s)).OrderByDescending(s => Dist2(s, _plan.Entrance)).ToList())
        {
            var ante = Attach(l, p, Room.Antechamber, main: true);
            var boss = ante == null ? null : Attach(l, ante, Room.Boss, main: true);
            if (boss != null)
            {
                return boss;
            }
            Drop(l, before);
        }
        return null;
    }
}
