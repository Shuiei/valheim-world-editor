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
                var c = Shaders.BiomeColor(s.Biomes[g]);
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

    /// <summary>The last area's ground and sea.</summary>
    private void DeleteTerrain()
    {
        _own.DeleteVertexArray(_terrainVao);
        _own.DeleteVertexArray(_waterVao);
        foreach (uint b in new[] { _terrainVbo, _terrainEbo, _terrainBiomeVbo, _terrainExtraVbo, _terrainCutVbo, _waterVbo, _waterEbo })
        {
            _own.DeleteBuffer(b);
        }
        _terrainVao = _waterVao = _terrainVbo = _terrainEbo = _terrainBiomeVbo = _terrainExtraVbo = _terrainCutVbo = _waterVbo = _waterEbo = 0;
    }

    /// <summary>
    /// The ground (one point per metre, with the game's terrain shader's extra values) and the sea, on the graphics
    /// card.
    /// </summary>
    private unsafe void BuildTerrain(WorldScene s)
    {
        DeleteTerrain();
        int w = s.W, h = s.H;
        float[] v = TerrainRows(s, 0, h - 1);
        uint[] idx = new uint[(w - 1) * (h - 1) * 6];
        int k = 0;
        for (int gz = 0; gz < h - 1; gz++)
        {
            for (int gx = 0; gx < w - 1; gx++)
            {
                uint a = (uint)(gz * w + gx), b = a + 1, c = a + (uint)w, d = c + 1;
                // Counter-clockwise seen from above (z mirrored).
                idx[k++] = a; idx[k++] = b; idx[k++] = c;
                idx[k++] = b; idx[k++] = d; idx[k++] = c;
            }
        }
        _terrainVao = _own.VertexArray();
        _gl.BindVertexArray(_terrainVao);
        uint vbo = _terrainVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        _own.Upload(BufferTargetARB.ArrayBuffer, v);
        uint ebo = _terrainEbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        _own.Upload(BufferTargetARB.ElementArrayBuffer, idx);
        for (uint a = 0; a < 3; a++)
        {
            _gl.EnableVertexAttribArray(a);
            _gl.VertexAttribPointer(a, 3, VertexAttribPointerType.Float, false, 36, (void*)(a * 12));
        }
        // For the game's terrain shader: biome colour (bytes), then mask uv, ocean depth, limit tint.
        uint bc = _terrainBiomeVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, bc);
        fixed (byte* p = s.BiomeColor)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)s.BiomeColor.Length, p, BufferUsageARB.StaticDraw);
        }
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.UnsignedByte, true, 4, (void*)0);
        float[] extra = ExtraRows(s, 0, h - 1);
        uint ex = _terrainExtraVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, ex);
        fixed (float* p = extra)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(extra.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribPointer(4, 2, VertexAttribPointerType.Float, false, 16, (void*)0);
        _gl.EnableVertexAttribArray(5);
        _gl.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, 16, (void*)8);
        _gl.EnableVertexAttribArray(6);
        _gl.VertexAttribPointer(6, 1, VertexAttribPointerType.Float, false, 16, (void*)12);
        // Where the ground is cut (none until the extensions' shapes are made: RebuildShapes).
        if (s.Cut.Length != w * h)
        {
            s.Cut = new float[w * h];
        }
        uint cv = _terrainCutVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, cv);
        fixed (float* p = s.Cut)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(s.Cut.Length * 4), p, BufferUsageARB.DynamicDraw);
        }
        _gl.EnableVertexAttribArray(7);
        _gl.VertexAttribPointer(7, 1, VertexAttribPointerType.Float, false, 4, (void*)0);
        _terrainIndexCount = (uint)idx.Length;
        // The sea: one quad over the area, at the water level.
        float hw = w / 2f + 400, hh = h / 2f + 400, y = s.Water;
        float[] q = [-hw, y, -hh, hw, y, -hh, hw, y, hh, -hw, y, hh];
        uint[] qi = [0, 2, 1, 0, 3, 2];
        (_waterVao, _waterVbo, _waterEbo) = _own.PositionMesh(q, qi);
    }
}
