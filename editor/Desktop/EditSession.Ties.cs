using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// Objects tied to a root that names them (ObjectTies): which things make one group, a selection bringing its groups
/// along, and a group moved as one block.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>The tie groups last worked out, with what they were worked out from.</summary>
    private ((int Objects, int Things, int Manifests) Of, List<(List<int> Group, TieRule Rule)> Groups)? _ties;

    /// <summary>
    /// The tie groups among the scene's things not gone (ObjectTies.Groups), each its root first, as indices into the
    /// things; worked out again when the objects, the things or the manifests changed.
    /// </summary>
    internal List<(List<int> Group, TieRule Rule)> TieGroups()
    {
        var rules = ObjectTies.Current;
        if (rules.Count == 0 || Scene.World is not { } world)
        {
            return [];
        }
        var list = Scene.Things;
        var of = (Edits.ObjectsVersion, list.Count, ModManifests.Generation);
        if (_ties is { } kept && kept.Of == of)
        {
            return kept.Groups;
        }
        var objects = new List<(int, ZdoData)>();
        lock (list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Gone && ObjectData.Bytes(world, Edits, list[i].Id) is { } bytes && ZdoData.Parse(bytes) is var z && ObjectTies.HoldsAny(z, rules))
                {
                    objects.Add((i, z));
                }
            }
        }
        var groups = ObjectTies.Groups(objects, rules);
        _ties = (of, groups);
        return groups;
    }

    /// <summary>
    /// Adds to a selection the rest of the tie groups it reaches: a group's root brings its members; with
    /// <paramref name="anyPart"/>, any member brings its whole group.
    /// </summary>
    /// <param name="list">The things, indices into the scene's things; added to.</param>
    /// <param name="parts">The things that are part of a group; added to.</param>
    /// <param name="nouns">What the groups are called; added to.</param>
    /// <param name="anyPart">Any member brings its group (else only its root).</param>
    /// <returns>How many groups the selection reaches.</returns>
    private int WithTied(List<int> list, HashSet<int> parts, HashSet<string> nouns, bool anyPart)
    {
        int groups = 0;
        var have = list.ToHashSet();
        foreach (var (group, rule) in TieGroups())
        {
            if (anyPart ? !group.Any(have.Contains) : !have.Contains(group[0]))
            {
                continue;
            }
            groups++;
            nouns.Add(rule.Name);
            foreach (int i in group)
            {
                parts.Add(i);
                if (have.Add(i))
                {
                    list.Add(i);
                }
            }
        }
        return groups;
    }

    /// <summary>
    /// Moves of things made moves of whole tie groups: the members of a group that the moves leave out go with the
    /// first of the group moved, as one block (where they stand from it kept, turned as it is turned).
    /// </summary>
    /// <param name="result">The moves (index into the scene's things, new place, turn and scale); added to.</param>
    private void TiedMoves(List<(int Index, Vector3 Position, Vector3 Rotation, float Scale)> result)
    {
        var things = Scene.Things;
        foreach (var (group, _) in TieGroups())
        {
            var moved = group.Select(i => result.FindIndex(m => m.Index == i)).Where(k => k >= 0).ToList();
            if (moved.Count == 0 || moved.Count == group.Count)
            {
                continue;
            }
            var m = result[moved[0]];
            var t = things[m.Index];
            var by = BlueprintFormats.FromEuler(m.Rotation) * Quaternion.Inverse(BlueprintFormats.FromEuler(t.Rotation));
            foreach (int i in group.Where(i => result.FindIndex(r => r.Index == i) < 0))
            {
                var p = things[i];
                result.Add((i, m.Position + Vector3.Transform(p.Position - t.Position, by), BlueprintFormats.ToEuler(by * BlueprintFormats.FromEuler(p.Rotation)), p.Scale));
            }
        }
    }
}
