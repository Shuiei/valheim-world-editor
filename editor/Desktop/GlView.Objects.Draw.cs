using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Drawing a batch's instances from another buffer, the material's uniforms, and the glass drawn last.
public sealed partial class GlView
{
    /// <summary>
    /// Draws a model part's instances from another buffer than its own (its material set up), then points its vertex
    /// array back at its own instances. With <paramref name="runs"/>, only the runs whose cell is in view are drawn
    /// (nothing at all, not even the material, when none is).
    /// </summary>
    /// <param name="b">The model part.</param>
    /// <param name="vbo">The buffer of the instances' matrices.</param>
    /// <param name="count">How many instances it holds.</param>
    /// <param name="runs">Its instances by area cell (see <see cref="Runs"/>), or null to draw them all.</param>
    /// <param name="u">The object shader's material uniforms.</param>
    private unsafe void DrawFrom(Batch b, uint vbo, int count, (int Cell, int Start, int Count)[]? runs, MaterialUniforms u)
    {
        if (runs != null && !runs.Any(r => Shows(r.Cell, float.MaxValue)))
        {
            return;
        }
        _gl.BindVertexArray(b.Vao);
        UseMaterial(b, u);
        if (runs == null)
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            PointInstances(0);
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)b.IndexCount, DrawElementsType.UnsignedInt, (void*)0, (uint)count);
        }
        else
        {
            // However far: these are not left out for being small and far away, only for being out of view.
            DrawRuns(b, vbo, count, runs, float.MaxValue, out _);
        }
        // The batch's own instances again.
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
        PointInstances(0);
        b.PointedAt = 0;
    }

    /// <summary>
    /// Sets the object shader up for a batch's material: its texture (looked up again until it is on the graphics card),
    /// its back faces culled or not (always for a mesh wound both ways), its colour, cutoff and uv, and whether it is
    /// blended (see-through).
    /// </summary>
    private void UseMaterial(Batch b, MaterialUniforms u)
    {
        var m = b.Material;
        if (b.Texture == 0 && m.Map != null)
        {
            lock (_textures)
            {
                _textures.TryGetValue(m.Map, out b.Texture);
            }
        }
        if (m.DoubleSided && !b.BothWays)
        {
            _gl.Disable(EnableCap.CullFace);
        }
        else
        {
            _gl.Enable(EnableCap.CullFace);
        }
        _gl.Uniform4(u.Color, m.Color.X, m.Color.Y, m.Color.Z, m.Color.W);
        _gl.Uniform1(u.Cutoff, m.Cutoff);
        _gl.Uniform1(u.HasMap, b.Texture != 0 ? 1 : 0);
        _gl.Uniform4(u.Uv, m.UvTransform.X, m.UvTransform.Y, m.UvTransform.Z, m.UvTransform.W);
        _gl.Uniform1(u.Blend, m.Transparent ? 1f : 0f);
        _gl.BindTexture(TextureTarget.Texture2D, b.Texture);
    }

    /// <summary>
    /// The things' see-through materials (glass), last of the objects: blended over what is drawn, behind them too, and
    /// not hiding what is drawn after. Buildings drawn see-through (View) had theirs drawn with them. Returns the
    /// instances drawn.
    /// </summary>
    private int DrawGlass(MaterialUniforms u, bool seeThrough)
    {
        int drawn = 0;
        bool started = false;
        foreach (var b in _batches)
        {
            if (!b.Material.Transparent || !_shown[(int)b.Kind] || seeThrough && b.Kind == ObjectKind.Buildings)
            {
                continue;
            }
            if (!started)
            {
                started = true;
                _gl.UseProgram(_objectProg);
                GlBlend.Over(_gl);
                _gl.DepthMask(false);
            }
            UseMaterial(b, u);
            _gl.BindVertexArray(b.Vao);
            drawn += DrawCells(b);
        }
        if (started)
        {
            _gl.DepthMask(true);
            _gl.Disable(EnableCap.Blend);
            _gl.BindVertexArray(0);
        }
        return drawn;
    }

    /// <summary>The object shader's uniforms a batch's material sets (their locations in the program).</summary>
    /// <param name="Color">The colour (uColor).</param>
    /// <param name="Cutoff">The alpha cutoff (uCutoff).</param>
    /// <param name="HasMap">Whether there is a texture (uHasMap).</param>
    /// <param name="Uv">The texture's scale and offset (uUv).</param>
    /// <param name="Blend">Whether the material is see-through (uBlend).</param>
    private readonly record struct MaterialUniforms(int Color, int Cutoff, int HasMap, int Uv, int Blend)
    {
        /// <summary>The locations in this program.</summary>
        public static MaterialUniforms Of(GlUniforms u, uint program) => new(u.At(program, "uColor"), u.At(program, "uCutoff"),
            u.At(program, "uHasMap"), u.At(program, "uUv"), u.At(program, "uBlend"));
    }
}
