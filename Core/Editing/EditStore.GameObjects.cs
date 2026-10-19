using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public sealed partial class EditStore
{
    /// <summary>
    /// Live: an object added in this session as the game has it now, once applied (its saved bytes, from a read of its
    /// zone). Where it stands, how it is turned, its size and its data replace the editor's copy under the same id,
    /// without a history step: the game's version is what undo takes away and redo brings back, and what a copy of
    /// it is made from. Nothing changes for an object not added now (deleted again, or never added) or one the game
    /// has as the editor does.
    /// </summary>
    /// <param name="id">The object's id (negative).</param>
    /// <param name="bytes">The object as the game saves it.</param>
    /// <returns>Whether the copy changed.</returns>
    public bool TakeFromGame(int id, byte[] bytes)
    {
        lock (_lock)
        {
            if (!_added.TryGetValue(id, out NewObject? n) || (n.Raw != null && n.Raw.AsSpan().SequenceEqual(bytes)))
            {
                return false;
            }
            var z = ZdoData.Parse(bytes);
            _added[id] = n with { Position = z.Position, Rotation = z.Rotation, Scale = ScaleOf(z), SourceId = null, Fresh = false, Raw = bytes, Data = null };
            Version++;
            ObjectsVersion++;
            return true;
        }
    }

    /// <summary>An object's own uniform size from its data ("scale", else "scaleScalar"); 0 when it has none.</summary>
    private static float ScaleOf(ZdoData z)
    {
        foreach (var (key, value) in z.Vec3List)
        {
            if (key == ZdoData.ScaleKey)
            {
                return value.X;
            }
        }
        foreach (var (key, value) in z.FloatList)
        {
            if (key == ZdoData.ScaleScalarKey)
            {
                return value;
            }
        }
        return 0f;
    }
}
