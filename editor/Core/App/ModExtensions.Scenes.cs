using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// What running extensions draw of an open area as a whole (ISceneProvider): meshes, and holes in the ground.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>Whether a running extension draws areas at all.</summary>
    public static bool AnyScenes => _state.Hooks.Any(l => l.Scenes.Count > 0);

    /// <summary>
    /// What the running extensions draw in an area, each layer checked: meshes whose triangles name corners they lack,
    /// or whose corners, normals or mask coordinates are not as many as the corners or not numbers, are left out, and
    /// so are empty holes. A provider that throws turns its extension off (the others go on).
    /// </summary>
    /// <param name="area">The area.</param>
    public static List<SceneLayer> ScenesOf(IAreaView area)
    {
        var layers = new List<SceneLayer>();
        foreach (var l in _state.Hooks)
        {
            foreach (var p in l.Scenes)
            {
                if (Guard(l, () => p.Build(area), null) is { } layer)
                {
                    layers.Add(Checked(layer));
                }
            }
        }
        return layers;
    }

    /// <summary>A layer with only what can be drawn.</summary>
    private static SceneLayer Checked(SceneLayer layer)
    {
        var ok = new SceneLayer();
        foreach (var m in (layer.Meshes ?? []).Where(m => m != null && ValidMesh(m)))
        {
            ok.Meshes.Add(m);
        }
        foreach (var h in (layer.Holes ?? []).Where(h => h != null && h.X1 > h.X0 && h.Z1 > h.Z0))
        {
            ok.Holes.Add(h);
        }
        return ok;
    }

    /// <summary>
    /// Whether a mesh can be drawn: whole triangles of corners it has, as many finite normals as finite corners, and
    /// biome colours, a mask of 1 to 256 texels a side and finite mask coordinates each as many as the corners when given.
    /// </summary>
    private static bool ValidMesh(SceneMesh m) =>
        m.Positions != null && m.Normals != null && m.Triangles != null && m.Normals.Length == m.Positions.Length
        && m.Triangles.Length % 3 == 0 && m.Triangles.All(i => i >= 0 && i < m.Positions.Length)
        && m.Positions.All(Finite) && m.Normals.All(Finite)
        && (m.BiomeColors == null || m.BiomeColors.Length == m.Positions.Length)
        && (m.Mask == null || m.Mask is { Width: >= 1 and <= 256, Height: >= 1 and <= 256 } mask && mask.Texels?.Length == mask.Width * mask.Height)
        && (m.MaskUvs == null || (m.MaskUvs.Length == m.Positions.Length && m.MaskUvs.All(u => float.IsFinite(u.X) && float.IsFinite(u.Y))));

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
