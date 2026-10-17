namespace TerrainEditor.Desktop;

/// <summary>
/// The highest top of the building pieces within a rectangle, for building on them.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>
    /// The highest top of the building pieces within a rectangle (0.6 m around it counts; whose names hold word, when
    /// given), or null.
    /// </summary>
    private float? HighestPieceTop(WorldScene scene, float x0, float z0, float x1, float z1, string? word = null)
    {
        float? best = null;
        lock (scene.Things)
        {
            foreach (var t in scene.Things)
            {
                if (t.Gone || !t.Piece || _w.NameOfPrefab(t.Prefab) is not string n || (word != null && !n.Contains(word, StringComparison.OrdinalIgnoreCase))
                    || Terrain.PieceCatalog.Get(t.Prefab) is not { } info)
                {
                    continue;
                }
                var p = t.Position;
                if (p.X < MathF.Min(x0, x1) - 0.6f || p.X > MathF.Max(x0, x1) + 0.6f || p.Z < MathF.Min(z0, z1) - 0.6f || p.Z > MathF.Max(z0, z1) + 0.6f)
                {
                    continue;
                }
                float top = p.Y + info.MaxY;
                best = best is float b ? MathF.Max(b, top) : top;
            }
        }
        return best;
    }
}
