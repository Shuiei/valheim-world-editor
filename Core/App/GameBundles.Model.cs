using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// Loading a prefab's model: its parts, placed from the walk over its objects.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// A prefab's model: what the game draws of it. Left out: the worn and broken looks of pieces
    /// (WearNTear), lower levels of detail, inactive objects, disabled renderers; for dungeon rooms and
    /// locations also their networked objects (saved objects of their own) and random parts not there half
    /// the time. Skinned meshes (animated stations) are drawn in their bind pose at the renderer.
    /// </summary>
    /// <param name="name">The prefab name.</param>
    /// <param name="room">A dungeon room or a location: leave out its networked objects and its random parts not
    /// there half the time.</param>
    /// <returns>The model, or null when the game has no such prefab.</returns>
    public Model? LoadModel(string name, bool room)
    {
        lock (_sync)
        {
            if (!EnsureIndex().TryGetValue(name, out var root))
            {
                return null;
            }
            try
            {
                return ReadModel(name, root, room);
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    /// <summary>
    /// Reads the model of a prefab's root: marks worn, broken and lower-detail parts (and for rooms their networked
    /// objects and random parts) to be skipped, then walks the active objects placing each renderer's submeshes.
    /// </summary>
    private Model ReadModel(string name, Where root, bool room)
    {
        var walk = new ModelWalk(this, root.Bundle);
        walk.Scan(root.PathId);
        if (room)
        {
            walk.RoomSkips(root.PathId, true);
        }
        walk.Walk(root.PathId, Matrix.Identity(), true);
        float[] rootScale = [1, 1, 1];
        if (Read(root.Bundle, root.PathId) is { } rgo && Components(root.Bundle, rgo).FirstOrDefault(c => c.Type == (int)AssetClassID.Transform).F is { } rt)
        {
            var s = rt["m_LocalScale"];
            rootScale = [s["x"].AsFloat, s["y"].AsFloat, s["z"].AsFloat];
        }
        return new Model(name, walk.Parts, rootScale);
    }
}
