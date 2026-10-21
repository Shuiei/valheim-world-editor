using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// The move in progress, or a new one of the selection; null when there is nothing to move (a runestone alone
    /// says why).
    /// </summary>
    private Move? Begin()
    {
        if (_move != null)
        {
            return _move;
        }
        var s = Scene;
        if (s == null)
        {
            return null;
        }
        var items = new List<Item>();
        int runestones = 0;
        // A linked group's part never moves alone: the whole group goes.
        var (chosen, parts, groups, noun) = Session?.WithLinked(_view.Selected) ?? ([.. _view.Selected], [], 0, LinkRule.DefaultName);
        foreach (int i in chosen)
        {
            var t = s.Things[i];
            if (t.Gone)
            {
                continue;
            }
            // A runestone's stone is built by the game from its location: it can only be deleted.
            if (t.Runestone)
            {
                runestones++;
                continue;
            }
            float[][]? snaps = PieceCatalog.Get(t.Prefab)?.Snaps;
            items.Add(new Item
            {
                Index = i,
                Thing = t,
                Lift = GroundAt(t.Position.X, t.Position.Z) is float g ? t.Position.Y - g : 0,
                Bottom = snaps is { Length: > 0 } ? snaps.Min(p => p[1]) : 0,
                Piece = IsPiece(t),
                Linked = parts.Contains(i),
            });
        }
        if (items.Count == 0)
        {
            if (runestones > 0)
            {
                Message?.Invoke(Said.Of("select.msg.runestones"), true);
            }
            return null;
        }
        return _move = new Move { Items = items, Cx = items.Average(it => it.Thing.Position.X), Cz = items.Average(it => it.Thing.Position.Z), Groups = groups, Noun = noun, ByClaude = ClaudeTools.Acting.Value };
    }

    /// <summary>
    /// What a linked group is called, in both languages: a mod's own name as it gives it, the editor's "group"
    /// from the catalogue.
    /// </summary>
    /// <param name="noun">The name the links give (<see cref="LinkRule.DefaultName"/> when the mod gave none).</param>
    private static Said Noun(string noun) => noun == LinkRule.DefaultName ? Said.Of("select.groupNoun") : Said.Raw(noun);
}
