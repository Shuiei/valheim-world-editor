using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// What the item and armour stands take in set_contents: their item, armour, orientation and pose.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>
    /// The stands' share of set_contents, into <paramref name="set"/>: an item stand's item (or its orientation alone) and an
    /// armour stand's armour (or its pose alone), each in the slot that takes it. Returns the items the stands did not take
    /// (null: all of them taken), or why the object refuses what was asked of it.
    /// </summary>
    /// <param name="prefab">The object's prefab.</param>
    /// <param name="name">Its name, for the answers.</param>
    /// <param name="items">The items given (null: none).</param>
    /// <param name="orientation">The item stand's orientation, if given.</param>
    /// <param name="pose">The armour stand's pose, if given.</param>
    /// <param name="set">The field changes so far; the stands' changes are added to it.</param>
    internal static (ItemSpec[]? Left, string? Refusal) StandChanges(int prefab, string name, ItemSpec[]? items, int? orientation, int? pose, List<FieldChange> set)
    {
        bool itemStand = StandData.IsItemStand(prefab), armourStand = StandData.IsArmourStand(prefab);
        if ((orientation != null && !itemStand) || (pose != null && !armourStand))
        {
            return (items, $"{name} is not {(orientation != null ? "an item stand: only item stands take an orientation" : "an armour stand: only armour stands take a pose")}.");
        }
        if (itemStand && (items != null || orientation != null))
        {
            if (items is { Length: > 1 })
            {
                return (items, $"{name} holds one item, not {items.Length}.");
            }
            if (items != null)
            {
                var held = items.Length == 0 ? null : new StandData.Held(items[0].Item, items[0].Quality, items[0].Variant);
                set.AddRange(StandData.ForItemStand(prefab, held, orientation ?? 0));
            }
            else
            {
                set.Add(new("ints", "type", Math.Max(0, orientation!.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            items = null;
        }
        if (armourStand && (items != null || pose != null))
        {
            if (items != null)
            {
                set.AddRange(StandData.ForArmourStand(prefab, [.. items.Select(i => new StandData.Held(i.Item, i.Quality, i.Variant))], pose));
            }
            else
            {
                set.Add(new("ints", "pose", Math.Max(0, pose!.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            items = null;
        }
        return (items, null);
    }
}
