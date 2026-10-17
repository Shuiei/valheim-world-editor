using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// A byte-array value as a running mod extension reads it (ModExtensions.Decode); null when none does (or for
    /// another kind of value).
    /// </summary>
    internal string? Decoded(Field f)
    {
        if (f.Section != "bytes" || _z is not { } z || z.GetBytes(f.Key) is not { } bytes)
        {
            return null;
        }
        var s = _session();
        WorldScene.Thing? t = Index is int ix && s != null && ix < s.Scene.Things.Count ? s.Scene.Things[ix] : null;
        var view = new ObjectView(PrefabCatalog.NameOf(z.Prefab) ?? "", t?.Position ?? z.Position, t?.Rotation ?? z.Rotation, z, t?.Scale ?? 1);
        return ModExtensions.Decode(f.Key, view, bytes);
    }
}
