using System.Numerics;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The shapes the mods' extensions draw for the area's things, and where they cut the ground. The steps are in
// GlView.Shapes.*.cs.
public sealed partial class GlView
{
    /// <summary>The batches of the mods' extensions' shapes (among <see cref="_batches"/>), made by RebuildShapes.</summary>
    private readonly List<Batch> _shapeBatches = [];
    /// <summary>The things changed: the extensions' shapes are made again at the next frame.</summary>
    private bool _shapesDirty = true;
    /// <summary>The extensions (ModExtensions.Generation) the shapes were made with.</summary>
    private int _shapesGeneration = -1;

    /// <summary>
    /// The scene's cut grid once the extensions' cuts were marked into it (null: none were, it holds zeros): the next
    /// rebuild clears it, and only then.
    /// </summary>
    private float[]? _cutMarked;

    /// <summary>
    /// The shapes the mods' running extensions draw for the area's things (ModExtensions.ShapesOf), made again: in view
    /// space, one batch per kind of object, colour and sidedness, each a single instance the object passes draw like any
    /// model (see-through ones with the glass). And where they cut the ground (ModExtensions.CutsOf): the grid points
    /// inside their outlines marked (WorldScene.Cut) and sent to the terrain. Their data read from the world and this
    /// session's objects.
    /// </summary>
    private unsafe void RebuildShapes(WorldScene s)
    {
        _shapesDirty = false;
        _shapesGeneration = ModExtensions.Generation;
        ClearShapeBatches();
        if (s.Cut.Length != s.W * s.H)
        {
            s.Cut = new float[s.W * s.H];
        }
        // Cleared only when cuts were marked into it (no extension cuts the ground: nothing to go over).
        bool wasCut = ReferenceEquals(_cutMarked, s.Cut);
        if (wasCut)
        {
            Array.Clear(s.Cut);
            _cutMarked = null;
        }
        if (s.World is not { } world || s.Session is not { } session || !ModExtensions.AnyShapes && !ModExtensions.AnyCuts)
        {
            SendCuts(s, wasCut);
            return;
        }
        List<WorldScene.Thing> all;
        lock (s.Things)
        {
            all = [.. s.Things.Where(t => !t.Gone)];
        }
        var things = all.Where(t => ModExtensions.HasShapes(t.Prefab) || ModExtensions.HasCuts(t.Prefab)).ToList();
        if (things.Count == 0)
        {
            SendCuts(s, wasCut);
            return;
        }
        var around = new ShapeWorld(world, session, all);
        var meshes = new Dictionary<(ObjectKind Kind, Vector4 Color, bool BothSides), (List<float> V, List<uint> I)>();
        foreach (var t in things)
        {
            if (ShapeWorld.ViewOf(world, session, t) is { } view)
            {
                AddThingShapes(s, t, view, around, meshes);
            }
        }
        _gl.BindVertexArray(0);
        foreach (var ((kind, color, both), (v, idx)) in meshes)
        {
            if (idx.Count != 0)
            {
                MakeShapeBatch(kind, color, both, v, idx);
            }
        }
        SendCuts(s, wasCut);
    }

    /// <summary>Deletes the batches of the extensions' shapes, made by the last rebuild.</summary>
    private void ClearShapeBatches()
    {
        foreach (var b in _shapeBatches)
        {
            _batches.Remove(b);
            _own.DeleteVertexArray(b.Vao);
            _own.DeleteBuffer(b.InstanceVbo);
            _own.DeleteBuffer(b.OwnVbo);
            _own.DeleteBuffer(b.OwnEbo);
        }
        _shapeBatches.Clear();
    }

    /// <summary>Sends the ground's cuts to the terrain, when there are any or there were.</summary>
    /// <param name="s">The scene.</param>
    /// <param name="wasCut">Whether the ground was cut before this rebuild.</param>
    private unsafe void SendCuts(WorldScene s, bool wasCut)
    {
        if (_terrainCutVbo == 0 || !wasCut && !ReferenceEquals(_cutMarked, s.Cut))
        {
            return;
        }
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _terrainCutVbo);
        fixed (float* p = s.Cut)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(s.Cut.Length * 4), p);
        }
    }
}
