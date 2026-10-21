using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>The boss's key, in a chest far from the boss's door; only when the settings ask for a boss key.</summary>
    private void PlaceBossKey()
    {
        if (!_s.BossKey || _plan.Boss == null)
        {
            return;
        }
        var dist = Distances(_plan.Boss, StairLinks(_plan.Spaces));
        var keyRoom = _plan.Spaces.Where(s => s.IsRoom && !s.Main && s.Room is not (Room.Secret or Room.StairTop or Room.StairBottom or Room.Treasury) && dist.ContainsKey(s))
            .OrderByDescending(s => dist[s] + _rnd.NextDouble()).FirstOrDefault()
            ?? _plan.Spaces.Where(s => s.IsRoom && s.Room is not (Room.Boss or Room.Treasury or Room.Antechamber or Room.StairTop or Room.StairBottom)).OrderByDescending(s => dist.GetValueOrDefault(s)).FirstOrDefault();
        keyRoom?.KeyChest = true;
    }

    /// <summary>A hidden cache or two (one more on a large dungeon), each attached to a room off the main way; only when the settings ask for secrets.</summary>
    private void PlaceSecretCaches()
    {
        if (!_s.Secrets)
        {
            return;
        }
        int want = 1 + (_plan.Spaces.Count(s => s.IsRoom) > 20 ? 1 : 0);
        for (int t = 0, made = 0; made < want && t < 30; t++)
        {
            var l = _plan.Levels[_rnd.Next(_plan.Levels.Count)];
            var parent = l.Spaces.Where(s => s.IsRoom && CanParent(s) && !s.Main).OrderBy(_ => _rnd.Next()).FirstOrDefault();
            if (parent != null && Attach(l, parent, Room.Secret, main: false, door: Door.Secret) != null)
            {
                made++;
            }
        }
    }
}
