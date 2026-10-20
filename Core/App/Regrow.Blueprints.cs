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
    /// blueprint's parts in its place. Each made object draws as it wakes (<see cref="WakeDraws"/>).
    /// </summary>
    private static void PlaceBlueprint(Rule veg, IReadOnlyList<BlueprintPart> parts, NVector3 at, Quaternion turn, List<Spot> spots)
    {
        float[] s = veg.scaleMinAxes != null && veg.scaleMaxAxes != null ? ScaleByAxes(veg) : [1f, 1f, 1f];
        PlaceParts(veg, parts, at, turn, new NVector3(s[0], s[1], s[2]), spots);
    }

    /// <summary>The parts of one blueprint at a place, turn and scale (Create.Blueprint, Create.SharedBPO).</summary>
    private static void PlaceParts(Rule veg, IReadOnlyList<BlueprintPart> parts, NVector3 at, Quaternion turn, NVector3 scale, List<Spot> spots)
    {
        foreach (var part in parts)
        {
            if (part.Chance < 1f && Rnd.Value > part.Chance)
            {
                continue;
            }
            var place = at + NVector3.Transform(part.Position, turn);
            var rotation = turn * part.Rotation;
            var size = scale * part.Scale;
            if (Terrain.PrefabCatalog.Get(Save.StableHash.Of(part.Prefab)) is not { } info)
            {
                // Another blueprint grows in its place; a part of no known prefab is left out (said when the file was read).
                if (veg.blueprints!.GetValueOrDefault(part.Prefab) is { } inner)
                {
                    PlaceParts(veg, inner, place, rotation, size, spots);
                }
                continue;
            }
            for (int k = WakeDraws(info); k > 0; k--)
            {
                Rnd.NextUInt();
            }
            var euler = BlueprintFormats.ToEuler(rotation);
            // The mod writes the scale only for an object whose network view keeps it (DataHelper.Init).
            var kept = info.SyncScale ? size : NVector3.One;
            spots.Add(new Spot(part.Prefab, place.X, place.Y, place.Z, euler.X, euler.Y, euler.Z, kept.X, kept.X == kept.Y && kept.Y == kept.Z ? null : [kept.X, kept.Y, kept.Z], Merged(veg.data, part.Data)));
        }
    }

    /// <summary>
    /// The draws an object makes as it wakes: its components' as the game's vegetation list counts them for its prefab
    /// (none when the list has no rule of it), and a building piece's two timers (WearNTear.Awake, without damage).
    /// </summary>
    private static int WakeDraws(Terrain.PrefabCatalog.Info info) =>
        (GameRules.FirstOrDefault(r => r.prefab == info.Name)?.draws ?? 0) + (info.WearHealth > 0f ? 2 : 0);

    /// <summary>The rule's data with a part's own over it (DataHelper.Merge); null when neither has any.</summary>
    private static IReadOnlyList<ContentField>? Merged(IReadOnlyList<ContentField>? rule, IReadOnlyList<ContentField>? own) =>
        rule == null ? own : own == null ? rule : [.. rule, .. own];
}
