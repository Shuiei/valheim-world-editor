using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// What a copy of an object starts from: the object it was copied from and the data it holds.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// The copy that stands for a thing moved, turned or sized (to Commit in place of it): it keeps the thing's own
    /// data (chest contents, builder…) and, for an object added in this session, what that was copied from and its
    /// values (the Workshop's objects and generated dungeons carry theirs there).
    /// </summary>
    /// <param name="index">The thing, an index into the scene's things.</param>
    /// <param name="position">Its new place.</param>
    /// <param name="rotation">Its new turn, Euler angles in degrees.</param>
    /// <param name="scale">Its new size (zero: the prefab's own).</param>
    internal (NewObject Object, bool Piece) MovedCopy(int index, System.Numerics.Vector3 position, System.Numerics.Vector3 rotation, float scale)
    {
        var t = Scene.Things[index];
        var from = t.Id < 0 ? Edits.FindAdded(t.Id) : null;
        return (new NewObject(0, t.Prefab, position, rotation, scale, t.Id < 0 ? from?.SourceId : t.Id, t.Id < 0 && (from?.Fresh ?? true), from?.Raw, from?.Data), t.Piece);
    }

    /// <summary>
    /// What a copy of a thing starts from: the saved object it comes from (its own id, or for one added in this session
    /// the id that was itself copied from; null: none) and everything it holds, builder included (its saved bytes read
    /// as fields; for an added one they cannot be read for, its own values; null: none), so a copy stays whole after
    /// the world is saved (the ids change) and in another world.
    /// </summary>
    /// <param name="t">The thing copied.</param>
    internal (int? SourceId, IReadOnlyList<ObjectField>? Data) CopySourceOf(WorldScene.Thing t)
    {
        var added = t.Id < 0 ? Edits.FindAdded(t.Id) : null;
        var data = Scene.World != null && ObjectData.Bytes(Scene.World, Edits, t.Id) is { } bytes ? ObjectData.Fields(bytes, builder: true) : added?.Data;
        return (t.Id >= 0 ? t.Id : added?.SourceId, data);
    }
}
