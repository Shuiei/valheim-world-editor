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
                start = PlaceEntrance(l);
            }
            // The way through: the style's main rooms in turn, then down, or to the boss.
            int mainCount = Math.Max(2, (int)MathF.Round(perLevel * 0.4f));
            var cur = BuildMainWay(l, start!, k, mainCount);
            if (!last)
            {
                var top = AttachLevelStairs(l, cur);
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
                PlaceArena(l, cur);
            }
            PlaceWings(l, perLevel - mainCount);
            AddLoops(l);
            AddStubs(l);
        }
        PlaceBossKey();
        PlaceSecretCaches();
        AssignDoors();
    }

    /// <summary>Adds the entrance room on the first level, centred on the level's front edge, and marks it as the way's start.</summary>
    /// <param name="l">The first level.</param>
    /// <returns>The entrance room.</returns>
    private DungeonPlan.Space PlaceEntrance(DungeonPlan.Level l)
    {
        var spec = SpecOf(Room.Entrance);
        int w = Even(spec.WMin, spec.WMax), d = Even(spec.DMin, spec.DMax);
        var start = NewSpace(l, Room.Entrance, (0, 1), Rect(-w / 2, 0, w, d));
        start.Main = true;
        _plan.Entrance = start;
        return start;
    }

    /// <summary>Attaches the level's main rooms in turn after <paramref name="start"/>, each one a main room of the style.</summary>
    /// <param name="l">The level.</param>
    /// <param name="start">The room the way starts from.</param>
    /// <param name="k">The level's index, which picks the style's main room to start with.</param>
    /// <param name="mainCount">How many main rooms to try.</param>
    /// <returns>The last room of the way.</returns>
    private DungeonPlan.Space BuildMainWay(DungeonPlan.Level l, DungeonPlan.Space start, int k, int mainCount)
    {
        var cur = start;
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
        return cur;
    }

    /// <summary>The stairs down from a level: off the way's end, else off the rooms that can lead on, furthest from the entrance first.</summary>
    /// <param name="l">The level.</param>
    /// <param name="cur">The way's last room.</param>
    /// <returns>The stairs' top, or null when no stairs fit.</returns>
    private DungeonPlan.Space? AttachLevelStairs(DungeonPlan.Level l, DungeonPlan.Space cur) =>
        AttachStairs(l, cur) ?? l.Spaces.Where(s => s.IsRoom && CanParent(s)).OrderByDescending(s => Dist2(s, _plan.Entrance)).Select(s => AttachStairs(l, s)).FirstOrDefault(s => s != null);
}
