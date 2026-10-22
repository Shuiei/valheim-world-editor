using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Moving linked groups as a whole: the root's move, and where the parts go with it.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Moves of things made moves of whole linked groups (ObjectLinks): a group's root goes where its move puts it (or,
    /// when only parts of it were moved, where the first of them takes it along), and its parts go along as one block,
    /// their offsets unchanged: stored at the root's new place with their own turn as it was when the mod saves them so
    /// (it builds the group there again), else where they are now drawn, moved as the root was. Other moves are left
    /// as they are; the ones given come first, in their order.
    /// </summary>
    /// <param name="moves">The moves (index into the scene's things, new place, turn and scale).</param>
    internal List<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale)> WholeGroupMoves(
        List<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale)> moves)
    {
        var links = ReadLinks();
        if (links.IsEmpty)
        {
            return moves;
        }
        var things = Scene.Things;
        var at = ShownById();
        var result = new List<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale)>(moves);
        var slot = new Dictionary<int, int>();
        for (int k = 0; k < result.Count; k++)
        {
            slot.TryAdd(result[k].Index, k);
        }
        void Set(int index, System.Numerics.Vector3 position, System.Numerics.Vector3 rotation, float scale)
        {
            if (slot.TryGetValue(index, out int k))
            {
                result[k] = (index, position, rotation, scale);
            }
            else
            {
                slot[index] = result.Count;
                result.Add((index, position, rotation, scale));
            }
        }
        var done = new HashSet<int>();
        foreach (var m in moves)
        {
            var t = things[m.Index];
            if (links.Get(t.Id) == null || links.Group(t.Id) is not { Count: > 1 } group || !done.Add(group[0])
                || !at.TryGetValue(group[0], out int rootIndex))
            {
                continue;
            }
            var root = things[rootIndex];
            System.Numerics.Vector3 position, rotation;
            float scale;
            if (slot.TryGetValue(rootIndex, out int k))
            {
                (_, position, rotation, scale) = result[k];
            }
            else
            {
                // The part's own move, from where it is drawn to where it goes, taken by the whole group.
                var by = BlueprintFormats.FromEuler(m.Rotation) * System.Numerics.Quaternion.Inverse(BlueprintFormats.FromEuler(t.Rotation));
                position = m.Position + System.Numerics.Vector3.Transform(root.Position - t.Position, by);
                rotation = BlueprintFormats.ToEuler(by * BlueprintFormats.FromEuler(root.Rotation));
                scale = root.Scale;
            }
            Set(rootIndex, position, rotation, scale);
            // The root's move, for the parts the mod saves where they are.
            var turn = BlueprintFormats.FromEuler(rotation) * System.Numerics.Quaternion.Inverse(BlueprintFormats.FromEuler(root.Rotation));
            foreach (int id in group.Skip(1))
            {
                if (!at.TryGetValue(id, out int i))
                {
                    continue;
                }
                var part = links.Get(id)!;
                float own = slot.TryGetValue(i, out int j) ? result[j].Scale : things[i].Scale;
                if (part.Link.Rule.StoredAtRoot)
                {
                    Set(i, position, part.Rotation, own);
                }
                else
                {
                    var p = things[i];
                    Set(i, position + System.Numerics.Vector3.Transform(p.Position - root.Position, turn),
                        BlueprintFormats.ToEuler(turn * BlueprintFormats.FromEuler(p.Rotation)), own);
                }
            }
        }
        return result;
    }
}
