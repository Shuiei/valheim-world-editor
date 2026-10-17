using TerrainEditor.App;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>Makes the whole plan into <see cref="_plan"/>.</summary>
    private void Build()
    {
        int levels = Math.Clamp(_s.Levels, 1, DungeonGen.MaxLevels);
        int perLevel = new[] { 5, 8, 12, 17 }[Math.Clamp(_s.Size, 1, 4) - 1];
        for (int k = 0; k < levels; k++)
        {
            _plan.Levels.Add(new DungeonPlan.Level { Index = k });
        }
        DungeonPlan.Space? start = null;
        for (int k = 0; k < levels; k++)
        {
            bool last = k == levels - 1;
            var l = _plan.Levels[k];
            if (k == 0)
            {
                var spec = SpecOf(Room.Entrance);
                int w = Even(spec.WMin, spec.WMax), d = Even(spec.DMin, spec.DMax);
                start = NewSpace(l, Room.Entrance, (0, 1), Rect(-w / 2, 0, w, d));
                start.Main = true;
                _plan.Entrance = start;
            }
            // The way through: the style's main rooms in turn, then down, or to the boss.
            int mainCount = Math.Max(2, (int)MathF.Round(perLevel * 0.4f));
            var cur = start!;
            for (int i = 0; i < mainCount; i++)
            {
                var room = _style.Main[(i + k) % _style.Main.Length];
                if (SpecOf(room).Unique && _used.Contains(room))
                {
                    room = Room.Hub;
                }
                var n = Attach(l, cur, room, main: true) ?? Attach(l, AnyParent(l, main: true), room, main: true);
                if (n != null)
                {
                    n.Main = true;
                    cur = n;
                }
            }
            if (!last)
            {
                var top = AttachStairs(l, cur) ?? l.Spaces.Where(s => s.IsRoom && CanParent(s)).OrderByDescending(s => Dist2(s, _plan.Entrance)).Select(s => AttachStairs(l, s)).FirstOrDefault(s => s != null);
                if (top == null)
                {
                    _plan.Notes.Add(Said.Of("dungeon.msg.noStairs", k + 1));
                    levels = k + 1;
                    _plan.Levels.RemoveRange(levels, _plan.Levels.Count - levels);
                    last = true;
                }
                else
                {
                    start = _plan.Levels[k + 1].Spaces[0];
                }
            }
            if (last)
            {
                // The antechamber is kept only with the arena beyond it: one with no arena would be a dead end.
                var before = Mark(l);
                var ante = Attach(l, cur, Room.Antechamber, main: true) ?? Attach(l, AnyParent(l, main: true), Room.Antechamber, main: true);
                var boss = ante == null ? null : Attach(l, ante, Room.Boss, main: true, forward: true) ?? Attach(l, ante, Room.Boss, main: true);
                if (boss == null)
                {
                    Drop(l, before);
                    // Not enough room ahead: anywhere it fits.
                    foreach (var p in l.Spaces.Where(s => s.IsRoom && CanParent(s)).OrderByDescending(s => Dist2(s, _plan.Entrance)).ToList())
                    {
                        ante = Attach(l, p, Room.Antechamber, main: true);
                        boss = ante == null ? null : Attach(l, ante, Room.Boss, main: true);
                        if (boss != null)
                        {
                            break;
                        }
                        Drop(l, before);
                    }
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
            // Rooms off the way: wings, some with a twin across, some leading further.
            int sides = perLevel - mainCount;
            DungeonPlan.Space? wing = null;
            for (int i = 0, tries = 0; i < sides && tries < sides * 6; tries++)
            {
                // Wings: a side room often grows off the last one, and what it is leans to what that one is (a kitchen
                // by the feast hall, cells by the guard room, an ossuary by the burial chamber: rooms that belong together).
                var parent = wing != null && CanParent(wing) && _rnd.NextDouble() < 0.55 ? wing : SideParent(l);
                if (parent == null)
                {
                    break;
                }
                var room = Pick([.. _style.Side.Select(s => (s.Room, s.Weight * (Kin(parent.Room).Contains(s.Room) ? 5f : 1f)))]);
                if (SpecOf(room).Unique && _used.Contains(room))
                {
                    continue;
                }
                var n = Attach(l, parent, room, main: false);
                if (n == null)
                {
                    continue;
                }
                wing = n;
                i++;
                if (_rnd.NextDouble() < _style.Symmetry && Twin(l, n) != null)
                {
                    i++;
                }
            }
            AddLoops(l);
            AddStubs(l);
        }
        // The boss's key, in a chest far from the boss's door; a hidden cache or two.
        if (_s.BossKey && _plan.Boss != null)
        {
            var dist = Distances(_plan.Boss, StairLinks(_plan.Spaces));
            var keyRoom = _plan.Spaces.Where(s => s.IsRoom && !s.Main && s.Room is not (Room.Secret or Room.StairTop or Room.StairBottom or Room.Treasury) && dist.ContainsKey(s))
                .OrderByDescending(s => dist[s] + _rnd.NextDouble()).FirstOrDefault()
                ?? _plan.Spaces.Where(s => s.IsRoom && s.Room is not (Room.Boss or Room.Treasury or Room.Antechamber or Room.StairTop or Room.StairBottom)).OrderByDescending(s => dist.GetValueOrDefault(s)).FirstOrDefault();
            keyRoom?.KeyChest = true;
        }
        if (_s.Secrets)
        {
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
        AssignDoors();
    }
}
