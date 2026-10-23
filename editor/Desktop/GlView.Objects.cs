using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// Drawing the objects beyond the batches in cells: the Place and Paste tools' ghosts, the dungeons' rooms and
// the locations' parts as solids, materials and see-through glass, where an object goes, and the support check's
// tints.
public sealed partial class GlView
{
    /// <summary>The buffer of the ghosts' and rooms' instances, made at first use.</summary>
    private uint _ghostVbo;
    /// <summary>The kinds the paste's ghost drew this frame (their posts are left out).</summary>
    private HashSet<int>? _pasteGhosts;
    /// <summary>
    /// The Place tool's preview, or the Paste tool's (the building where it would land), drawn with the
    /// models, see-through. Kinds whose model is not there yet keep their posts (DrawPlace, the paste's
    /// outline). Kinds no model draws but a mod's extension does are drawn by its shapes. Returns the prefabs drawn.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    private unsafe HashSet<int> DrawGhosts(WorldScene s, Matrix4x4 vp)
    {
        var drawn = new HashSet<int>();
        List<(int Prefab, Vector3 Position, Vector3 Rotation, float Scale)> shown;
        if (Mode == ToolMode.Place && Place is { } pl && pl.Shown.Length > 0)
        {
            shown = [.. pl.Shown.Select(o => (Save.StableHash.Of(o.Name), o.Position, o.Rotation, o.Scale))];
        }
        else if (Mode == ToolMode.Paste && Paste.At is { } pat && Paste.Clip != null)
        {
            Paste.MaskAt = PasteMask(s);
            shown = Paste.Ghosts(pat, GridHeight, s.OriginX, s.OriginZ);
        }
        else
        {
            return drawn;
        }
        _pasteGhosts = Mode == ToolMode.Paste ? drawn : null;
        DrawGhostInstances(s, shown, drawn);
        if (TerrainEditor.App.ModExtensions.AnyShapes)
        {
            DrawShapeGhosts(s, shown, vp, drawn);
        }
        return drawn;
    }

    /// <summary>
    /// The Place and Paste previews: models at places that are not things of the scene, see-through, one draw per model
    /// part. Kinds whose model is not read yet, or that have none, are skipped (and left out of
    /// <paramref name="drawn"/>).
    /// </summary>
    private void DrawGhostInstances(WorldScene s, IEnumerable<(int Prefab, Vector3 Position, Vector3 Rotation, float Scale)> shown, HashSet<int>? drawn = null)
    {
        _gl.UseProgram(_objectProg);
        _gl.Uniform1(_u.At(_objectProg, "uGhost"), 1f);
        var u = MaterialUniforms.Of(_u, _objectProg);
        // Ghosts are see-through whole.
        GlBlend.Over(_gl);
        _gl.DepthMask(false);
        foreach (var byKind in shown.GroupBy(o => o.Prefab))
        {
            int prefab = byKind.Key;
            var (piece, g) = PieceAndGroup(prefab);
            if (!g.Ready || g.Batches.Count == 0)
            {
                continue;
            }
            drawn?.Add(prefab);
            var placements = byKind.Select(o => Placement(s, new WorldScene.Thing(0, prefab, o.Position, o.Rotation, o.Scale, piece), g.RootScale)).ToList();
            foreach (var (b, pre) in g.Batches)
            {
                DrawPlaced(b, [.. placements.Select(m => pre * m)], u);
            }
        }
        _gl.Uniform1(_u.At(_objectProg, "uGhost"), 0f);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// A model part drawn at these placements (not things of the scene: the ghosts, the selection's mask), through the
    /// ghosts' buffer, made at first use.
    /// </summary>
    /// <param name="b">The model part.</param>
    /// <param name="data">Its placements.</param>
    /// <param name="u">The object shader's material uniforms.</param>
    private unsafe void DrawPlaced(Batch b, Matrix4x4[] data, MaterialUniforms u)
    {
        if (_ghostVbo == 0)
        {
            _ghostVbo = _own.Buffer();
        }
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _ghostVbo);
        fixed (Matrix4x4* p = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), p, BufferUsageARB.DynamicDraw);
        }
        DrawFrom(b, _ghostVbo, data.Length, null, u);
    }

    /// <summary>
    /// Points the bound vertex array's instance matrix (attributes 3 to 6, one 64-byte matrix each) into the bound
    /// buffer from this byte on.
    /// </summary>
    /// <param name="offset">The first matrix's byte in the buffer.</param>
    private unsafe void PointInstances(long offset)
    {
        for (uint c = 0; c < 4; c++)
        {
            _gl.VertexAttribPointer(3 + c, 4, VertexAttribPointerType.Float, false, 64, (void*)(offset + c * 16));
        }
    }

    /// <summary>
    /// Sets a new vertex array's instance matrix up: attributes 3 to 6 on, one step per instance, pointed at the start
    /// of the bound buffer.
    /// </summary>
    private void SetUpInstances()
    {
        for (uint c = 0; c < 4; c++)
        {
            _gl.EnableVertexAttribArray(3 + c);
            _gl.VertexAttribDivisor(3 + c, 1);
        }
        PointInstances(0);
    }

    /// <summary>
    /// Whether a prefab is a building piece (not a plain object), and its group of drawn things (see KindGroup).
    /// </summary>
    /// <param name="prefab">The prefab's id.</param>
    private (bool Piece, Group G) PieceAndGroup(int prefab)
    {
        bool piece = Terrain.PieceCatalog.Get(prefab)?.Tool != null;
        return (piece, KindGroup(prefab, piece));
    }

    /// <summary>
    /// The Mask's weight at a grid point of a scene's ground, as a paste applies it (1 off the ground); null without a
    /// session or with the Mask off.
    /// </summary>
    /// <param name="s">The scene shown.</param>
    private static Func<int, int, float>? PasteMask(WorldScene s)
    {
        if (s.Session is not { } session || session.MaskNow() is not { } mask)
        {
            return null;
        }
        var g = session.Ground;
        return (x, z) => x < 0 || z < 0 || x >= g.W || z >= g.H ? 1 : mask(z * g.W + x);
    }
}
