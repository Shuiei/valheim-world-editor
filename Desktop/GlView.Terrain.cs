using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// The ground and the water on the graphics card: built for a new area, its rows sent again as they change.
public sealed partial class GlView
{
    /// <summary>The ground's and the water's vertex arrays and buffers, and the ground's index count.</summary>
    private uint _terrainVao, _terrainIndexCount, _waterVao, _terrainVbo, _terrainExtraVbo;
    /// <summary>More of the ground's and the water's buffers.</summary>
    private uint _terrainEbo, _terrainBiomeVbo, _waterVbo, _waterEbo;
    /// <summary>One float per grid point: 1 where a mod's extension cuts the ground (GroundCuts), sent with the shapes.</summary>
    private uint _terrainCutVbo;
    /// <summary>The scene changed: the ground and the water are built again at the next frame.</summary>
    private bool _sceneDirty;

    // ---- The ground: one point per metre, normals from the neighbours, colour from the biome.
    /// <summary>Position, normal and colour of rows z0..z1 (9 floats a point).</summary>
    internal static float[] TerrainRows(WorldScene s, int z0, int z1)
    {
        float[] v = new float[s.W * (z1 - z0 + 1) * 9];
        TerrainPoints(s, 0, z0, s.W - 1, z1, v);
        return v;
    }

    /// <summary>Position, normal and colour of the points x0..x1 of rows z0..z1 (9 floats a point), row after row, into v.</summary>
    internal static void TerrainPoints(WorldScene s, int x0, int z0, int x1, int z1, float[] v)
    {
        int w = s.W, h = s.H, n = x1 - x0 + 1;
        float H(int x, int z) => s.Heights[Math.Clamp(z, 0, h - 1) * w + Math.Clamp(x, 0, w - 1)];
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                int g = gz * w + gx, o = ((gz - z0) * n + gx - x0) * 9;
                var nv = Vector3.Normalize(new Vector3(H(gx - 1, gz) - H(gx + 1, gz), 2f, -(H(gx, gz - 1) - H(gx, gz + 1))));
                var c = s.GroundColor(g);
                v[o] = gx - (w - 1) / 2f; v[o + 1] = s.Heights[g]; v[o + 2] = -(gz - (h - 1) / 2f);
                v[o + 3] = nv.X; v[o + 4] = nv.Y; v[o + 5] = nv.Z;
                v[o + 6] = c.X; v[o + 7] = c.Y; v[o + 8] = c.Z;
            }
        }
    }

    /// <summary>Mask uv, ocean depth and limit tint of rows z0..z1 (4 floats a point).</summary>
    internal static float[] ExtraRows(WorldScene s, int z0, int z1)
    {
        float[] extra = new float[s.W * (z1 - z0 + 1) * 4];
        ExtraPoints(s, 0, z0, s.W - 1, z1, extra);
        return extra;
    }

    /// <summary>Mask uv, ocean depth and limit tint of the points x0..x1 of rows z0..z1 (4 floats a point), into extra.</summary>
    internal static void ExtraPoints(WorldScene s, int x0, int z0, int x1, int z1, float[] extra)
    {
        int w = s.W, h = s.H, o = 0;
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++, o += 4)
            {
                int g = gz * w + gx;
                extra[o] = (gx + 0.5f) / w;
                extra[o + 1] = (gz + 0.5f) / h;
                extra[o + 2] = s.OceanDepth[g];
                extra[o + 3] = s.Limit[g];
            }
        }
    }

    /// <summary>The changed ground's vertices and extra values on their way to the graphics card, kept from step to step.</summary>
    private float[] _rowsBuffer = [], _extraBuffer = [];

    /// <summary>The most grid points whose upload buffers are kept (a 256 m square): larger changes are rare.</summary>
    internal const int KeptUploadPoints = 256 * 256;

    /// <summary>
    /// After an edit: the changed points (and their neighbours, whose normals move) to the graphics card, row by row
    /// (all of a row's points at once when the rectangle is as wide as the area).
    /// </summary>
    private unsafe void UpdateTerrain(WorldScene s, (int X0, int Z0, int X1, int Z1) d)
    {
        int x0 = Math.Max(0, d.X0 - 1), x1 = Math.Min(s.W - 1, d.X1 + 1);
        int z0 = Math.Max(0, d.Z0 - 1), z1 = Math.Min(s.H - 1, d.Z1 + 1);
        int n = x1 - x0 + 1, rows = z1 - z0 + 1;
        // Kept for brush-sized changes; a bigger one (the whole area read again) gets buffers of its own.
        float[] vs = _rowsBuffer, extras = _extraBuffer;
        if (vs.Length < n * rows * 9)
        {
            (vs, extras) = (new float[n * rows * 9], new float[n * rows * 4]);
            if (n * rows <= KeptUploadPoints)
            {
                (_rowsBuffer, _extraBuffer) = (vs, extras);
            }
        }
        TerrainPoints(s, x0, z0, x1, z1, vs);
        ExtraPoints(s, x0, z0, x1, z1, extras);
        // Whole rows are one run in the buffers; a narrower rectangle is a run per row.
        int runs = n == s.W ? 1 : rows, perRun = n == s.W ? n * rows : n;
        fixed (float* v = vs, extra = extras)
        {
            for (int r = 0; r < runs; r++)
            {
                int first = (z0 + r) * s.W + x0;
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _terrainVbo);
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, first * 36, (nuint)(perRun * 36), v + r * perRun * 9);
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _terrainExtraVbo);
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, first * 16, (nuint)(perRun * 16), extra + r * perRun * 4);
            }
        }
        _look?.UpdateRows(s, x0, z0, x1, z1);
    }

    /// <summary>The last area's ground and sea, and the mods' layers over it.</summary>
    private void DeleteTerrain()
    {
        DropLayers();
        _own.DeleteVertexArray(_terrainVao);
        _own.DeleteVertexArray(_waterVao);
        foreach (uint b in new[] { _terrainVbo, _terrainEbo, _terrainBiomeVbo, _terrainExtraVbo, _terrainCutVbo, _waterVbo, _waterEbo })
        {
            _own.DeleteBuffer(b);
        }
        _terrainVao = _waterVao = _terrainVbo = _terrainEbo = _terrainBiomeVbo = _terrainExtraVbo = _terrainCutVbo = _waterVbo = _waterEbo = 0;
    }
}
