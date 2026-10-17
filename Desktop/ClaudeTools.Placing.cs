using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The placed objects a new piece could snap to, for place_pieces.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>
    /// The objects of the area a new piece could snap to: those within the snap search (10 m beyond the longest piece's
    /// reach) of the pieces asked for. The pieces whose place is not a number are not counted, so they never snap.
    /// </summary>
    /// <param name="scene">The open area.</param>
    /// <param name="pieces">The pieces asked for.</param>
    internal List<Editing.Hammer.Placed> PlacedNear(WorldScene scene, IReadOnlyList<PieceSpec> pieces)
    {
        var placed = new List<Editing.Hammer.Placed>();
        // Only the pieces a new one could snap to: within the snap search (10 m beyond the longest piece's reach) of
        // the pieces asked for. One whose place is not a number never snaps.
        var asked = pieces.Where(p => float.IsFinite(p.X) && float.IsFinite(p.Z)).ToList();
        float reach = Editing.Hammer.SnapRadius + Editing.Hammer.MaxRadius() + 1f;
        (float lx, float hx, float lz, float hz) = asked.Count == 0 ? (0f, -1f, 0f, -1f)
            : (asked.Min(p => p.X) - reach, asked.Max(p => p.X) + reach, asked.Min(p => p.Z) - reach, asked.Max(p => p.Z) + reach);
        lock (scene.Things)
        {
            for (int i = 0; i < scene.Things.Count; i++)
            {
                var t = scene.Things[i];
                if (!t.Gone && t.Position.X >= lx && t.Position.X <= hx && t.Position.Z >= lz && t.Position.Z <= hz
                    && _w.NameOfPrefab(t.Prefab) is string n && Editing.Hammer.Get(n) != null)
                {
                    placed.Add(new(i, n, t.Position, BlueprintFormats.FromEuler(t.Rotation)));
                }
            }
        }
        return placed;
    }
}
