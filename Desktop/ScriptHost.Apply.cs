using Microsoft.CodeAnalysis;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// Putting a run's changes into the area: the snapping of new pieces, and the one undo step.
public static partial class ScriptHost
{
    /// <summary>
    /// The new pieces placed with snap moved to meet the snap points of the pieces standing (where the script left
    /// them; those it took away do not count) and of those placed before them, as the game's hammer does.
    /// </summary>
    /// <param name="snap">The area as the script saw it.</param>
    /// <param name="ch">The script's changes; its new pieces are moved in place.</param>
    /// <param name="cancel">Stop: checked before each piece (thousands take a while).</param>
    /// <exception cref="ArgumentException">A piece snaps to a place outside the area.</exception>
    internal static void SnapPieces(Snapshot snap, Changes ch, CancellationToken cancel = default)
    {
        if (!ch.Add.Any(a => a.Snap))
        {
            return;
        }
        var placed = new List<Hammer.Placed>();
        foreach (var t in snap.Things)
        {
            if (!ch.Remove.Contains(t.Index) && Hammer.Get(t.Prefab) != null)
            {
                var (p, r, _) = ch.Moves.TryGetValue(t.Index, out var m) ? m : (t.Position, t.Rotation, t.Scale);
                placed.Add(new(t.Index, t.Prefab, p, BlueprintFormats.FromEuler(r)));
            }
        }
        for (int i = 0; i < ch.Add.Count; i++)
        {
            cancel.ThrowIfCancellationRequested();
            var a = ch.Add[i];
            var q = BlueprintFormats.FromEuler(a.Rotation);
            if (a.Snap && Hammer.Get(a.Name) != null)
            {
                var to = Hammer.Snap(a.Name, q, a.Position, placed).Position;
                InArea(snap, to.X, to.Z, $"the {a.Name} placed at ({a.Position.X}, {a.Position.Z}) snaps to ({to.X}, {to.Z}), which");
                ch.Add[i] = a with { Position = to };
            }
            placed.Add(new(-1 - i, a.Name, ch.Add[i].Position, q));
        }
    }

    /// <summary>
    /// The script's changes into the area, one undo step. Returns how many points the game's ±8 m stopped.
    /// </summary>
    public static int Apply(EditSession s, Changes ch, string label)
    {
        int clamped = 0;
        var adds = ch.Add.Select(a => (new NewObject(0, a.Prefab, a.Position, a.Rotation, a.Scale), PieceCatalog.Get(a.Prefab)?.Tool != null)).ToList();
        // Moved things: replaced by copies where they go (those also taken away are only taken away).
        var moved = ch.Moves.Where(m => !ch.Remove.Contains(m.Key) && m.Key >= 0 && m.Key < s.Scene.Things.Count && !s.Scene.Things[m.Key].Gone).ToList();
        adds.AddRange(moved.Select(m => s.MovedCopy(m.Key, m.Value.Position, m.Value.Rotation, m.Value.Scale)));
        s.Commit(label, g =>
        {
            var touched = new List<int>();
            bool was = g.NoLimit;
            g.NoLimit = ch.NoLimit;
            try
            {
                foreach (var (p, h) in ch.Heights)
                {
                    if (g.SetHeight(p, h))
                    {
                        clamped++;
                    }
                    touched.Add(p);
                }
            }
            finally
            {
                g.NoLimit = was;
            }
            foreach (var (p, colour) in ch.Paint)
            {
                Array.Copy(colour, 0, g.Paint, p * 4, 4);
                g.PMod[p] = 1;
                touched.Add(p);
            }
            return (touched, touched.Count == 0 ? (0, 0, 0, 0) : (touched.Min(p => p % g.W) - 1, touched.Min(p => p / g.W) - 1, touched.Max(p => p % g.W) + 1, touched.Max(p => p / g.W) + 1));
        }, [.. ch.Remove, .. moved.Select(m => m.Key)], adds);
        return clamped;
    }
}
