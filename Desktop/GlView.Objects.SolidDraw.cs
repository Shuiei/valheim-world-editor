using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// Drawing the solid models: opaque parts first, then the see-through ones (glass), in the cells in view.
public sealed partial class GlView
{
    /// <summary>Whether a kind's model is drawn solid: read, with parts.</summary>
    private static bool DrawnSolid(Group g) => g.Ready && g.Batches.Count > 0 && g.HasModel;

    /// <summary>
    /// Draws models solid at places that are not things of the scene: opaque parts first, then see-through ones
    /// (glass) blended over them, only in the area's cells in view. Their instances are made again (<c>MakeSolid</c>)
    /// only when <paramref name="source"/> is another list, the area or its cells changed, or one of its kinds' models
    /// was read or let go since; kinds whose model is not read yet, or that have none, are skipped.
    /// </summary>
    /// <param name="s">The area.</param>
    /// <param name="source">The list the places come from, the same object until they change.</param>
    /// <param name="shown">The places, taken from <paramref name="source"/> only when the instances are made again.</param>
    /// <param name="made">The instances made last from this kind of list.</param>
    private void DrawSolid(WorldScene s, object source, Func<IEnumerable<(int Prefab, Vector3 Position, Vector3 Rotation)>> shown, Solid made)
    {
        EnsureCells(s);
        bool stale = made.Source != source || made.Scene != s || made.Cells != _cellsMade;
        for (int k = 0; k < made.Kinds.Count && !stale; k++)
        {
            var (prefab, piece, group, drawn) = made.Kinds[k];
            var g = KindGroup(prefab, piece);
            stale = g != group || DrawnSolid(g) != drawn;
        }
        if (stale)
        {
            MakeSolid(s, source, shown(), made);
            // The cells' boxes grew (or are new): which are in view is seen again, so no part in view is left out.
            SeeCells(_lastViewProj, _lastEye);
        }
        if (made.Draws.Count == 0)
        {
            return;
        }
        _gl.UseProgram(_objectProg);
        _gl.Uniform1(_u.At(_objectProg, "uGhost"), 0f);
        var u = MaterialUniforms.Of(_u, _objectProg);
        foreach (bool glass in (ReadOnlySpan<bool>)[false, true])
        {
            if (glass)
            {
                GlBlend.Over(_gl);
                _gl.DepthMask(false);
            }
            foreach (var (b, vbo, count, runs) in made.Draws)
            {
                if (b.Material.Transparent == glass)
                {
                    DrawFrom(b, vbo, count, runs, u);
                }
            }
        }
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
        _gl.BindVertexArray(0);
    }
}
