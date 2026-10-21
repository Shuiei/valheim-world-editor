using System.Numerics;
using ValheimGen;
using NVector3 = System.Numerics.Vector3;
using Rnd = ValheimGen.UnityEngine.Random;
using Rule = ValheimGen.VegetationRule;

namespace TerrainEditor.App;

// Expand World Data's blueprint vegetation: a blueprint grown in place of an object.
public static partial class Regrow
{
    /// <summary>
    /// A blueprint grown at a spot as the mod spawns it (VegetationSpawning.InstantiateBlueprint, Create.Blueprint): the
    /// rule's scale by axis drawn first (whatever the game's uniform draw), then each part made by chance, turned and
    /// scaled with the blueprint, given the rule's data with its own over it; a part that is a blueprint grows that
    /// blueprint's parts in its place. Each made object draws as it wakes (<see cref="Wake"/>, in the biome where the
    /// blueprint grows).
    /// </summary>
    private static void PlaceBlueprint(Rule veg, IReadOnlyList<BlueprintPart> parts, NVector3 at, Quaternion turn, int biome, List<Spot> spots)
    {
        float[] s = veg.scaleMinAxes != null && veg.scaleMaxAxes != null ? ScaleByAxes(veg) : [1f, 1f, 1f];
        PlaceParts(veg, parts, new Placed(at, turn, new NVector3(s[0], s[1], s[2]), biome), spots);
    }

    /// <summary>Where a blueprint's parts go: its place, turn and scale, and the biome it grows in.</summary>
    /// <param name="At">Its place.</param>
    /// <param name="Turn">Its turn.</param>
    /// <param name="Scale">Its scale on each axis.</param>
    /// <param name="Biome">The biome it grows in.</param>
    private readonly record struct Placed(NVector3 At, Quaternion Turn, NVector3 Scale, int Biome);

    /// <summary>The parts of one blueprint at a place, turn and scale (Create.Blueprint, Create.SharedBPO).</summary>
    private static void PlaceParts(Rule veg, IReadOnlyList<BlueprintPart> parts, Placed blueprint, List<Spot> spots)
    {
        foreach (var part in parts)
        {
            if (part.Chance < 1f && Rnd.Value > part.Chance)
            {
                continue;
            }
            var place = blueprint.At + NVector3.Transform(part.Position, blueprint.Turn);
            var rotation = blueprint.Turn * part.Rotation;
            var size = blueprint.Scale * part.Scale;
            if (Terrain.PrefabCatalog.Get(Save.StableHash.Of(part.Prefab)) is not { } info)
            {
                // Another blueprint grows in its place; a part of no known prefab is left out (said when the file was read).
                if (veg.blueprints!.GetValueOrDefault(part.Prefab) is { } inner)
                {
                    PlaceParts(veg, inner, blueprint with { At = place, Turn = rotation, Scale = size }, spots);
                }
                continue;
            }
            var data = Merged(veg.data, part.Data);
            Wake(part.Prefab, blueprint.Biome, data);
            var euler = BlueprintFormats.ToEuler(rotation);
            // The mod writes the scale only for an object whose network view keeps it (DataHelper.Init).
            var kept = info.SyncScale ? size : NVector3.One;
            spots.Add(new Spot(part.Prefab, place.X, place.Y, place.Z, euler.X, euler.Y, euler.Z, kept.X, kept.X == kept.Y && kept.Y == kept.Z ? null : [kept.X, kept.Y, kept.Z], data));
        }
    }

    /// <summary>The rule's data with a part's own over it (DataHelper.Merge); null when neither has any.</summary>
    private static IReadOnlyList<ContentField>? Merged(IReadOnlyList<ContentField>? rule, IReadOnlyList<ContentField>? own) =>
        rule == null ? own : own == null ? rule : [.. rule, .. own];
}
