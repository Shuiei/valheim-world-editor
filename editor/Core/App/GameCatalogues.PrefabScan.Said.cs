using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class PrefabScan
    {
        /// <summary>
        /// What a script read anywhere says, taken as it is read (the script itself is not kept: items'
        /// are large): its GameObject, what a sapling grows into, an item's entry (without "at", "ats"),
        /// what an item stand takes (types, items taken and refused), an armour stand's slots.
        /// </summary>
        /// <param name="Go">The script's GameObject.</param>
        /// <param name="Grows">A sapling's grown prefabs.</param>
        /// <param name="Item">An item's entry.</param>
        /// <param name="Stand">An item stand's types, items taken and items refused.</param>
        /// <param name="Slots">An armour stand's slots, each the types it takes.</param>
        private sealed record Said(long Go, List<Ref>? Grows, Dict? Item, (List<long> Types, List<Ref> Take, List<Ref> Refuse)? Stand, List<object?>? Slots);

        /// <summary>
        /// What a script says, by its roles (see <c>Said</c>).
        /// </summary>
        private static Said Say(Bundle.Inner f, Role r, AssetTypeValueField d)
        {
            Dict? item = (r & Role.Drop) != 0 ? ItemOf(d["m_itemData"]["m_shared"]) : null;
            return new Said(
                d["m_GameObject.m_PathID"].AsLong,
                (r & Role.Plant) != 0 ? RefsIn(f, d, "m_grownPrefabs") : null,
                item,
                item == null && (r & Role.Stand) != 0 ? (TypesOf(d["m_supportedTypes"]), RefsIn(f, d, "m_supportedItems"), RefsIn(f, d, "m_unsupportedItems")) : null,
                item == null && (r & Role.Stand) == 0 && (r & Role.Armour) != 0 ? [.. d["m_slots.Array"].Children.Select(s => (object?)TypesOf(s["m_supportedTypes"]))] : null);
        }

        /// <summary>
        /// The sorted distinct types of a list of enum values.
        /// </summary>
        private static List<long> TypesOf(AssetTypeValueField list) => [.. list["Array"].Children.Select(t => Int(Py(t))).Distinct().Order()];

        /// <summary>
        /// The references of a named list field of an object, when it has one.
        /// </summary>
        private static List<Ref> RefsIn(Bundle.Inner f, AssetTypeValueField d, string name) => Has(d, name) ? RefsOf(f, d[name]["Array"].Children) : [];

        /// <summary>
        /// The references of these pointers (those that resolve; the others left out).
        /// </summary>
        private static List<Ref> RefsOf(Bundle.Inner f, IEnumerable<AssetTypeValueField> pptrs) => [.. pptrs.Select(p => Bundle.RefOf(f, p)).OfType<Ref>()];
    }
}
