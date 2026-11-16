using System.Numerics;

namespace TerrainEditor.Desktop;

// The Landform tool's preview: the landform as it would rise (or sink), its lines kept while the pointer stays on one
// grid point.
public sealed partial class GlView
{
    /// <summary>The landform preview's lines, kept while the pointer stays on the same grid point.</summary>
    private float[] _previewSegs = [];
    /// <summary>The grid point (and area size) the landform preview was made for; null to make it again.</summary>
    private (int X, int Z, int W, int H)? _previewKey;

    /// <summary>Whether a Mountain click at a grid point fits in the open area (what EditSession.Mountain needs).</summary>
    internal static bool MountainFits(WorldScene s, float gx, float gz, float reach) => gx - reach >= 1 && gz - reach >= 1 && gx + reach <= s.W - 2 && gz + reach <= s.H - 2;

    /// <summary>
    /// Draws the landform preview at the grid point under the pointer, made again when the point changes.
    /// </summary>
    /// <param name="s">The area.</param>
    /// <param name="vp">The camera's view × projection.</param>
    /// <param name="h">The grid point under the pointer.</param>
    /// <param name="shape">The metres the landform adds at each offset from the point.</param>
    private void DrawLandformPreview(WorldScene s, Matrix4x4 vp, (float X, float Z) h, Func<float, float, float> shape)
    {
        var key = ((int)MathF.Round(h.X), (int)MathF.Round(h.Z), s.W, s.H);
        if (_previewKey != key)
        {
            _previewKey = key;
            _previewSegs = MountainMesh(s, key.Item1, key.Item2, Landform.Reach, shape, Landform.Least);
        }
        DrawLines(ref _previewVao, ref _previewVbo, _previewSegs, vp, new Vector4(1, 0.78f, 0.35f, 0.9f), width: 1f);
    }

    /// <summary>
    /// A landform as it would rise (or sink) at grid point (cx, cz): lines along x and z every few metres, each
    /// point at the ground plus the shape's height; ground it hardly moves (by <paramref name="least"/> or less,
    /// up or down) left out.
    /// </summary>
    /// <param name="s">The area.</param>
    /// <param name="cx">The middle, a grid x.</param>
    /// <param name="cz">The middle, a grid z.</param>
    /// <param name="reach">How far the mesh reaches around the middle, in metres.</param>
    /// <param name="shape">The metres added at each offset from the middle.</param>
    /// <param name="least">The least change, in metres, a point must have to be drawn.</param>
    internal static float[] MountainMesh(WorldScene s, int cx, int cz, float reach, Func<float, float, float> shape, float least = 0.5f)
    {
        // Lines about 2 m apart, at most 40 each way (a mountain's are further apart) and at least 12 (a small shape).
        int cells = Math.Clamp((int)MathF.Ceiling(reach), 12, 40);
        float step = 2 * reach / cells;
        float[] y = new float[(cells + 1) * (cells + 1)];
        bool[] up = new bool[y.Length];
        for (int j = 0; j <= cells; j++)
        {
            for (int i = 0; i <= cells; i++)
            {
                float dx = -reach + i * step, dz = -reach + j * step;
                float add = shape(dx, dz);
                y[j * (cells + 1) + i] = OnGround(s, cx + dx, cz + dz, 0).Y + add + 0.3f;
                up[j * (cells + 1) + i] = MathF.Abs(add) > least;
            }
        }
        var data = new List<float>();
        void Seg(int i0, int j0, int i1, int j1)
        {
            int a = j0 * (cells + 1) + i0, b = j1 * (cells + 1) + i1;
            if (!up[a] && !up[b])
            {
                return;
            }
            var (ax, az) = GridToView(s, cx - reach + i0 * step, cz - reach + j0 * step);
            var (bx, bz) = GridToView(s, cx - reach + i1 * step, cz - reach + j1 * step);
            data.AddRange([ax, y[a], az, bx, y[b], bz]);
        }
        for (int j = 0; j <= cells; j++)
        {
            for (int i = 0; i <= cells; i++)
            {
                if (i < cells)
                {
                    Seg(i, j, i + 1, j);
                }
                if (j < cells)
                {
                    Seg(i, j, i, j + 1);
                }
            }
        }
        return [.. data];
    }
}
