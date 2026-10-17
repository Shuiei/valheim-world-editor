using System.Numerics;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Drawing the objects: their shader's uniforms, and the batches in their passes (opaque, then see-through).
public sealed partial class GlView
{
    /// <summary>
    /// Sets the object shader's uniforms for the frame (the matrix, the sun, the fog, the eye, the cut), and the cells
    /// the view is in when the cells are shown.
    /// </summary>
    /// <param name="s">The area shown.</param>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    /// <param name="eye">The eye's position.</param>
    /// <returns>The material uniforms, to draw the batches with.</returns>
    private unsafe MaterialUniforms SetObjectUniforms(WorldScene s, Matrix4x4 vp, Vector3 eye)
    {
        var sun = GameLookGl.SunDirView;
        _gl.UseProgram(_objectProg);
        _gl.UniformMatrix4(_u.At(_objectProg, "uViewProj"), 1, false, (float*)&vp);
        _gl.Uniform3(_u.At(_objectProg, "uSun"), sun.X, sun.Y, sun.Z);
        void V3(string n, Vector3 v) => _gl.Uniform3(_u.At(_objectProg, n), v.X, v.Y, v.Z);
        V3("uSunColor", GameLookGl.SunColor);
        V3("uAmbient", GameLookGl.Ambient);
        V3("uFogColor", GameLookGl.FogColor);
        V3("uSunFogColor", GameLookGl.SunFogColor);
        _gl.Uniform1(_u.At(_objectProg, "uFogDensity"), GameLookGl.FogDensity);
        _gl.Uniform1(_u.At(_objectProg, "uFogStart"), GameLookGl.FogStart);
        V3("uEye", eye);
        var u = MaterialUniforms.Of(_u, _objectProg);
        _gl.Uniform1(_u.At(_objectProg, "uMap"), 0);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.Uniform2(_u.At(_objectProg, "uCut"), CutY != null ? 1f : 0f, CutY ?? 0f);
        if (_cellScene == s)
        {
            SeeCells(vp, eye);
        }
        return u;
    }

    /// <summary>
    /// Draws the batches the shown kinds have: the opaque ones first; the see-through buildings after, blended over
    /// everything and not hiding what is behind them (when the see-through view is on).
    /// </summary>
    /// <param name="u">The material uniforms.</param>
    /// <param name="seeThrough">Whether the see-through view is on.</param>
    /// <returns>The instances drawn by the batches.</returns>
    private int DrawObjectBatches(MaterialUniforms u, bool seeThrough)
    {
        int uSee = _u.At(_objectProg, "uSeeThrough");
        int drawn = 0;
        _drawCalls = 0;
        // Opaque first; see-through buildings after, blended over everything and not hiding
        // what is behind them; see-through materials (glass) last (DrawGlass).
        foreach (bool pass in seeThrough ? new[] { false, true } : [false])
        {
            if (pass)
            {
                GlBlend.Over(_gl);
                _gl.DepthMask(false);
                _gl.Uniform1(uSee, 1f);
            }
            foreach (var b in _batches)
            {
                if (!_shown[(int)b.Kind] || seeThrough && (b.Kind == ObjectKind.Buildings) != pass || !pass && b.Material.Transparent)
                {
                    continue;
                }
                UseMaterial(b, u);
                _gl.BindVertexArray(b.Vao);
                drawn += DrawCells(b);
            }
            if (pass)
            {
                _gl.Uniform1(uSee, 0f);
                _gl.DepthMask(true);
                _gl.Disable(EnableCap.Blend);
            }
        }
        return drawn;
    }
}
