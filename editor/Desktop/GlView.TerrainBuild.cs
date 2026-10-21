using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

public sealed partial class GlView
{
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
        // For the game's terrain shader: biome colour (bytes), then mask uv, ocean depth, limit tint.
        (_terrainVbo, _terrainEbo, _terrainBiomeVbo, _terrainExtraVbo) = UploadGroundLayout(v, idx, s.BiomeColor, ExtraRows(s, 0, h - 1));
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
