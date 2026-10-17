using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

/// <summary>
/// The game's look, as the web editor's terrain/look.js draws it: Valheim's own terrain shader (read from the game's
/// files, see GameLookData) lit like the game's deferred renderer, the ocean and a sky with clouds, at midday in the
/// default "Clear" weather. Inside the shaders everything is in Unity's left-handed world space: view (x, y, z) is
/// Unity (x + Cx, y, Cz - z).
/// </summary>
public sealed partial class GameLookGl
{
    /// <summary>
    /// The view's OpenGL (set by Init).
    /// </summary>
    private GL _gl = null!;
    /// <summary>
    /// Everything made on the graphics card, deleted together by Delete.
    /// </summary>
    private GlObjects _own = null!;
    /// <summary>The programs' uniform locations, asked once each; made again with the programs (Init).</summary>
    private GlUniforms _u = null!;
    /// <summary>
    /// The three programs.
    /// </summary>
    private uint _terrain, _sky, _water;
    /// <summary>
    /// The sky sphere's mesh and index count, and the water plane's mesh.
    /// </summary>
    private uint _skyVao, _skyCount, _waterVao;
    /// <summary>
    /// The terrain shader's textures by their uniform's name, bound to units in this order by UseTerrain.
    /// </summary>
    private readonly Dictionary<string, (uint Tex, TextureTarget Target)> _tex = [];
    /// <summary>
    /// The area's paint mask and its heights (for the water's depth), updated by UpdateRows.
    /// </summary>
    private uint _maskTex, _heightTex;
    /// <summary>
    /// The area drawn (its offset, size and water level).
    /// </summary>
    private WorldScene _scene = null!;

    /// <summary>
    /// Sends the shaders, textures and meshes to the graphics card for an area. The view's context must be current.
    /// </summary>
    /// <param name="gl">The view's OpenGL.</param>
    /// <param name="program">Compiles and links a program from vertex and fragment source.</param>
    /// <param name="f">The game's files (Read).</param>
    /// <param name="s">The area to draw.</param>
    public void Init(GL gl, Func<string, string, uint> program, Files f, WorldScene s)
    {
        _gl = gl;
        _own = new GlObjects(gl);
        _u = new GlUniforms(gl);
        _scene = s;
        // Rows of odd widths (the grid's 64n + 1 points, as half floats) are not padded to 4 bytes.
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        // The extracted shader brings its own precision lines (GLSL ES); desktop OpenGL ignores them.
        _terrain = _own.Program(program(TerrainVs, f.Fragment.Replace("#version 300 es", "") + Common + TerrainMain));
        _sky = _own.Program(program(SkyVs, Common + SkyFs));
        _water = _own.Program(program(WaterVs, Common + WaterFs));
        _tex["_DiffuseArrayTex"] = (Array2D(f.DiffuseArray, srgb: true), TextureTarget.Texture2DArray);
        _tex["_NormalArrayTex"] = (Array2D(f.NormalArray, srgb: false), TextureTarget.Texture2DArray);
        _tex["_NoiseTex"] = (Tex2D(f.Noise), TextureTarget.Texture2D);
        _tex["_CliffNormal"] = (Tex2D(f.Cliff), TextureTarget.Texture2D);
        _tex["_MistlandsCliffNormal"] = (Tex2D(f.MistCliff), TextureTarget.Texture2D);
        _tex["_PavedNormal"] = (Tex2D(f.Paved), TextureTarget.Texture2D);
        _tex["_RockNormal"] = (Tex2D(f.Rock), TextureTarget.Texture2D);
        _tex["_SnowNormal"] = (Tex2D(f.Snow), TextureTarget.Texture2D);
        _tex["_ColorVarietyNoise"] = (Tex2D(f.Variety), TextureTarget.Texture2D);
        _tex["_SkyAlphaTexture"] = (Tex2D(new Image(1, 1, [255, 255, 255, 255])), TextureTarget.Texture2D);
        _tex["_CurlNoise"] = (Noise3D(32, 7), TextureTarget.Texture3D);
        _tex["_SnowGlintOffsetNoise"] = (Noise3D(32, 11), TextureTarget.Texture3D);
        _maskTex = Tex2D(new Image(s.W, s.H, s.Mask), mipmaps: false, repeat: false);
        _tex["_ClearedMaskTex"] = (_maskTex, TextureTarget.Texture2D);
        _heightTex = HeightTexture(s);
        BuildSky();
        BuildWater(s);
    }

    /// <summary>
    /// Its programs, textures and meshes off the graphics card (another area, or the view's context going). The
    /// drawing's context must be current.
    /// </summary>
    public void Delete()
    {
        _own?.DeleteAll();
        _tex.Clear();
        _terrain = _sky = _water = _skyVao = _waterVao = _maskTex = _heightTex = 0;
    }

    /// <summary>The changed paint mask and heights on their way to the graphics card, kept from edit to edit.</summary>
    private byte[] _maskRows = [];
    /// <summary>See <see cref="_maskRows"/>.</summary>
    private Half[] _heightRows = [];

    /// <summary>
    /// After an edit: the paint mask and the heights (the water's depth) of grid points x0..x1 of rows z0..z1
    /// (included) again.
    /// </summary>
    public unsafe void UpdateRows(WorldScene s, int x0, int z0, int x1, int z1)
    {
        int n = x1 - x0 + 1, rows = z1 - z0 + 1;
        // Kept for brush-sized changes (as the view's own, GlView.KeptUploadPoints); a bigger one gets its own.
        byte[] mask = _maskRows;
        Half[] heights = _heightRows;
        if (heights.Length < n * rows)
        {
            (mask, heights) = (new byte[n * rows * 4], new Half[n * rows]);
            if (n * rows <= GlView.KeptUploadPoints)
            {
                (_maskRows, _heightRows) = (mask, heights);
            }
        }
        for (int r = 0; r < rows; r++)
        {
            int from = (z0 + r) * s.W + x0;
            Array.Copy(s.Mask, from * 4, mask, r * n * 4, n * 4);
            for (int i = 0; i < n; i++)
            {
                heights[r * n + i] = (Half)s.Heights[from + i];
            }
        }
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        _gl.BindTexture(TextureTarget.Texture2D, _maskTex);
        fixed (byte* p = mask)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, x0, z0, (uint)n, (uint)rows, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        _gl.BindTexture(TextureTarget.Texture2D, _heightTex);
        fixed (Half* p = heights)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, x0, z0, (uint)n, (uint)rows, PixelFormat.Red, PixelType.HalfFloat, p);
        }
    }

    // ---- Drawing. eye: the camera in view space; time: seconds since the start (water, clouds, glints).
    /// <summary>
    /// Uses a program and sets the uniforms all three share (fog, sun, area offset, camera, sky colours, Unity's
    /// _Time).
    /// </summary>
    /// <param name="prog">The program.</param>
    /// <param name="vp">View × projection.</param>
    /// <param name="eye">The camera in view space.</param>
    /// <param name="time">Seconds since the start (water, clouds, glints).</param>
    private unsafe void Shared(uint prog, Matrix4x4 vp, Vector3 eye, float time)
    {
        _gl.UseProgram(prog);
        void V3(string n, Vector3 v) => _gl.Uniform3(_u.At(prog, n), v.X, v.Y, v.Z);
        _gl.UniformMatrix4(_u.At(prog, "uViewProj"), 1, false, (float*)&vp);
        V3("uFogColor", FogColor);
        V3("uSunFogColor", SunFogColor);
        _gl.Uniform1(_u.At(prog, "uFogDensity"), FogDensity);
        _gl.Uniform1(_u.At(prog, "uFogStart"), FogStart);
        V3("uSunDir", SunDir);
        V3("uSunColor", SunColor);
        V3("uOffset", new Vector3(_scene.Cx, 0, _scene.Cz));
        V3("_WorldSpaceCameraPos", new Vector3(eye.X + _scene.Cx, eye.Y, _scene.Cz - eye.Z));
        V3("uEye", eye);
        V3("uTop", SkyTop);
        V3("uHorizon", SkyHorizon);
        _gl.Uniform4(_u.At(prog, "_Time"), time / 20, time, time * 2, time * 3);
    }

    /// <summary>
    /// Draws the sky behind everything, at the far plane (without writing depth).
    /// </summary>
    /// <param name="vp">View × projection.</param>
    /// <param name="eye">The camera in view space.</param>
    /// <param name="time">Seconds since the start (water, clouds, glints).</param>
    public unsafe void DrawSky(Matrix4x4 vp, Vector3 eye, float time)
    {
        Shared(_sky, vp, eye, time);
        _gl.DepthMask(false);
        _gl.Disable(EnableCap.CullFace);
        _gl.BindVertexArray(_skyVao);
        _gl.DrawElements(PrimitiveType.Triangles, _skyCount, DrawElementsType.UnsignedInt, (void*)0);
        _gl.DepthMask(true);
    }

    /// <summary>
    /// Binds the terrain shader with its textures and settings; the caller draws the ground mesh.
    /// </summary>
    /// <param name="vp">View × projection.</param>
    /// <param name="eye">The camera in view space.</param>
    /// <param name="time">Seconds since the start (water, clouds, glints).</param>
    /// <param name="slope">View: slope colours.</param>
    /// <param name="contour">View: contour lines every so many metres (0: none).</param>
    /// <param name="limit">View: Limit marks (the points at the game's ±8 m edit limit).</param>
    public void UseTerrain(Matrix4x4 vp, Vector3 eye, float time, bool slope, float contour, bool limit)
    {
        Shared(_terrain, vp, eye, time);
        void F(string n, float v) => _gl.Uniform1(_u.At(_terrain, n), v);
        _gl.Uniform4(_u.At(_terrain, "unity_SHAr"), 0, 0, 0, Ambient.X);
        _gl.Uniform4(_u.At(_terrain, "unity_SHAg"), 0, 0, 0, Ambient.Y);
        _gl.Uniform4(_u.At(_terrain, "unity_SHAb"), 0, 0, 0, Ambient.Z);
        _gl.Uniform3(_u.At(_terrain, "_SkyAlphaPosition"), 0, -1e5f, 0);
        F("_SnowGlintSize", 0.2f); F("_SnowGlintDensity", 0.15f); F("_SnowGlintStrength", 4);
        F("_SnowGlintNoiseDensity", 1); F("_SnowGlintNoiseAmount", 80); F("_SnowGlintViewAmount", 0.5f); F("_SnowGlintTimeAmount", 3);
        _gl.Uniform3(_u.At(_terrain, "_AshlandsVariationCol"), 0.4104f, 0.4838f, 0.5472f);
        F("_Glossiness", 0.1f); F("_SnowGloss", 1); F("_RockGloss", 0.7f); F("_Metallic", 0);
        F("_WaterLevel", _scene.Water); F("_LodHideDistance", 1e6f); F("_LodHideModifier", 0);
        F("_UVScale", 0.5f); F("_BumpScale", 1); F("_Wet", 0);
        F("uSlope", slope ? 1 : 0); F("uContour", contour); F("uLimit", limit ? 1 : 0);
        int unit = 0;
        foreach (var (name, (tex, target)) in _tex)
        {
            _gl.ActiveTexture(TextureUnit.Texture0 + unit);
            _gl.BindTexture(target, tex);
            _gl.Uniform1(_u.At(_terrain, name), unit);
            unit++;
        }
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    /// <summary>
    /// Draws the water, blended over the ground (without writing depth).
    /// </summary>
    /// <param name="vp">View × projection.</param>
    /// <param name="eye">The camera in view space.</param>
    /// <param name="time">Seconds since the start (water, clouds, glints).</param>
    public unsafe void DrawWater(Matrix4x4 vp, Vector3 eye, float time)
    {
        Shared(_water, vp, eye, time);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _heightTex);
        _gl.Uniform1(_u.At(_water, "uHeight"), 0);
        _gl.Uniform4(_u.At(_water, "uRegion"), _scene.OriginX, _scene.OriginZ, _scene.W, _scene.H);
        _gl.Uniform1(_u.At(_water, "uWater"), _scene.Water);
        _gl.Disable(EnableCap.CullFace);
        GlBlend.Over(_gl);
        _gl.DepthMask(false);
        _gl.BindVertexArray(_waterVao);
        _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
    }
}
