using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Decides what stands in each doorway: the key gate (or a grand one) before the arena, a gate to the
    /// treasury and on the way between levels, the style's doors elsewhere.
    /// </summary>
    private void AssignDoors()
    {
        foreach (var d in _plan.Doors)
        {
            var rooms = new[] { d.A, d.B };
            if (d.Kind == Door.Secret)
            {
                continue;
            }
            if (rooms.Any(r => r.Room == Room.Boss) && rooms.Any(r => r.Room == Room.Antechamber || r.Corridor && r.Parent?.Room == Room.Antechamber))
            {
                d.Kind = _s.BossKey ? Door.Key : Door.Grand;
                continue;
            }
            if (rooms.Any(r => r.Room == Room.Treasury))
            {
                d.Kind = Door.Gate;
                continue;
            }
            if (rooms.Any(r => r.Room == Room.Secret))
            {
                d.Kind = Door.Open;
                continue;
            }
            if (rooms.Any(r => r.Room == Room.Cells) && _rnd.NextDouble() < 0.6)
            {
                d.Kind = Door.Gate;
                continue;
            }
            bool grand = rooms.Any(r => r.Room is Room.Hall or Room.Throne or Room.Feast or Room.Chapel);
            var kind = Pick(_style.Doors);
            if (grand && _rnd.NextDouble() < 0.35)
            {
                kind = Door.Grand;
            }
            if (kind == Door.Grand && !rooms.Any(r => r.Room is Room.Hall or Room.Throne or Room.Feast or Room.Chapel or Room.Hub or Room.Boss))
            {
                kind = Door.Wood;
            }
            d.Kind = kind;
        }
        // Doors on the way between levels, so what lives below stays below.
        foreach (var top in _plan.Spaces.Where(s => s.Room == Room.StairTop))
        {
            foreach (var d in top.Doors.Where(d => d.Kind is Door.Open or Door.Curtain))
            {
                d.Kind = Door.Gate;
            }
        }
    }
}
