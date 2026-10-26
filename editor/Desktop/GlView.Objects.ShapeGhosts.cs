using System.Numerics;
using Silk.NET.OpenGL;
using TerrainEditor.App;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.Desktop;

// The Place and Paste previews of objects the editor has no model of but a mod's extension draws (ModExtensions.ShapesOf):
// their shapes, see-through, where each would land.
public sealed partial class GlView
{
    /// <summary>The see-through shade of a preview drawn by an extension's shapes.</summary>
    private const float ShapeGhostAlpha = 0.55f;

    /// <summary>The extensions' shapes of a kind with none of its data, by prefab, with the extensions they were made with.</summary>
    private readonly Dictionary<int, (int Generation, Shape[] Shapes)> _ghostShapes = [];

    /// <summary>The previews' vertex array and buffer.</summary>
    private uint _shapeGhostVao, _shapeGhostVbo;

    /// <summary>
    /// The previews whose kind no model drew, drawn by their extension's shapes instead: each object's shapes (as the
    /// extension draws one of its prefab with none of its data) turned, sized and placed as it would land, see-through.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="shown">The previews.</param>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    /// <param name="drawn">The prefabs drawn; those drawn here are added.</param>
    private void DrawShapeGhosts(WorldScene s, IEnumerable<(int Prefab, Vector3 Position, Vector3 Rotation, float Scale)> shown, Matrix4x4 vp, HashSet<int> drawn)
    {
        var byColour = new Dictionary<Vector4, List<float>>();
        foreach (var (prefab, position, rotation, scale) in shown)
        {
            if (drawn.Contains(prefab) || !ModExtensions.HasShapes(prefab) || GhostShapes(prefab) is not { Length: > 0 } shapes)
            {
                continue;
            }
            var turn = BlueprintFormats.FromEuler(rotation);
            float size = scale == 0 ? 1 : scale;
            foreach (var shape in shapes)
            {
                var colour = shape.Color with { W = shape.Color.W * ShapeGhostAlpha };
                var tris = byColour.TryGetValue(colour, out var list) ? list : byColour[colour] = [];
                foreach (int k in shape.Triangles)
                {
                    var v = SceneLayers.ToView(s, position + Vector3.Transform(shape.Vertices[k] * size, turn));
                    tris.AddRange([v.X, v.Y, v.Z]);
                }
            }
        }
        foreach (var (colour, tris) in byColour)
        {
            DrawLines(ref _shapeGhostVao, ref _shapeGhostVbo, [.. tris], vp, colour, PrimitiveType.Triangles, blend: true);
        }
        foreach (var (prefab, _, _, _) in shown)
        {
            if (_ghostShapes.TryGetValue(prefab, out var kept) && kept.Shapes.Length > 0)
            {
                drawn.Add(prefab);
            }
        }
    }

    /// <summary>
    /// The shapes the running extensions draw for an object of a prefab with none of its data, in its own frame (those in
    /// the world's left out), made once while the extensions stay the same.
    /// </summary>
    private Shape[] GhostShapes(int prefab)
    {
        int generation = ModExtensions.Generation;
        if (_ghostShapes.TryGetValue(prefab, out var kept) && kept.Generation == generation)
        {
            return kept.Shapes;
        }
        var view = new ObjectView(Terrain.PrefabCatalog.NameOf(prefab) ?? "", Vector3.Zero, Vector3.Zero, new Save.ZdoData { Prefab = prefab });
        Shape[] shapes = [.. ModExtensions.ShapesOf(prefab, view, ModExtensions.NoWorld).Where(sh => !sh.WorldSpace)];
        _ghostShapes[prefab] = (generation, shapes);
        return shapes;
    }
}
