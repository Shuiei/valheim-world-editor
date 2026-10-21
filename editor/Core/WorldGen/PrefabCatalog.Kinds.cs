using TerrainEditor.App;

namespace TerrainEditor.Terrain;

public static partial class PrefabCatalog
{
    /// <summary>
    /// What an item is, for the stands that hold items.
    /// </summary>
    /// <param name="Type">Its type (ItemDrop.ItemData.ItemType).</param>
    /// <param name="AttachOverride">The type it attaches as when that differs (atgeirs).</param>
    /// <param name="Variants">Its number of variants (shield styles).</param>
    /// <param name="Attach">Whether it has an "attach" child (item stands take no item without).</param>
    /// <param name="AttachSkin">Whether it has an "attach_skin" child (armour stands take either, or chest and legs
    /// pieces without).</param>
    /// <param name="Durability">For items that wear, their durability when new at quality 1 (0 for items that do
    /// not wear).</param>
    /// <param name="DurabilityPerLevel">What each further quality level adds to it.</param>
    /// <param name="MaxStack">The largest stack of the item (1 for items that do not stack).</param>
    public sealed record ItemKind(int Type, int AttachOverride, int Variants, bool Attach, bool AttachSkin, float Durability = 0, float DurabilityPerLevel = 0, int MaxStack = 1)
    {
        /// <summary>
        /// A new item's durability at this quality (ItemDrop.ItemData.GetMaxDurability); 100 for items that do not
        /// wear.
        /// </summary>
        public float NewDurability(int quality) => Durability > 0 ? Durability + Math.Max(0, quality - 1) * DurabilityPerLevel : 100f;
    }

    /// <summary>
    /// An item stand's rule (ItemStand.CanAttach): items of these types, and these items whatever their
    /// type, but never the refused ones.
    /// </summary>
    /// <param name="Types">Item types (ItemDrop.ItemData.ItemType) it takes.</param>
    /// <param name="Takes">Item prefab names it takes whatever their type.</param>
    /// <param name="Refuses">Item prefab names it never takes.</param>
    public sealed record StandRule(IReadOnlySet<int> Types, IReadOnlySet<string> Takes, IReadOnlySet<string> Refuses);

    /// <summary>
    /// What a spawner makes: an invisible spawn point (CreatureSpawner, or a TriggerSpawner an event wakes) or a
    /// nest (SpawnArea: a greydwarf nest, a bone pile...) that spawns around itself.
    /// </summary>
    /// <param name="Creatures">The creatures' prefab names (one for a CreatureSpawner).</param>
    /// <param name="Nest">A nest (SpawnArea), not a spawn point.</param>
    /// <param name="Trigger">A spawn point an event wakes (TriggerSpawner), not one that spawns by itself.</param>
    /// <param name="RespawnMinutes">A CreatureSpawner's time before its creature comes back once dead; 0 when it
    /// spawns its creature once.</param>
    /// <param name="MinLevel">The lowest level (1 = no star).</param>
    /// <param name="MaxLevel">The highest level, as the game's data has it (see <see cref="Levels"/>).</param>
    /// <param name="Day">Spawns by day.</param>
    /// <param name="Night">Spawns by night.</param>
    /// <param name="Radius">A nest's radius it spawns in.</param>
    /// <param name="MaxNear">How many creatures a nest keeps around.</param>
    public sealed record SpawnKind(IReadOnlyList<string> Creatures, bool Nest, bool Trigger, float RespawnMinutes, int MinLevel, int MaxLevel, bool Day, bool Night, float Radius, int MaxNear)
    {
        /// <summary>
        /// Whether the creature comes back after it dies.
        /// </summary>
        public bool Respawns => !Nest && !Trigger && RespawnMinutes > 0;

        /// <summary>
        /// The levels the creature can have, as the game rolls them (CreatureSpawner.Spawn): from the lowest up to
        /// the highest, and none above 1 when the highest is 1 (some spawners have 3 to 1: always level 1).
        /// </summary>
        public (int Min, int Max) Levels => MaxLevel > 1 ? (Math.Min(MinLevel, MaxLevel), MaxLevel) : (1, 1);

        /// <summary>
        /// One line on what it spawns: "Boar · 0–2 ★ · spawns once", "Draugr · 0–2 ★ · back 60 min after it dies ·
        /// by night only", "Greydwarf, Greydwarf_Elite · up to 3 around, within 5 m". In the language in use, or in
        /// English for Claude.
        /// </summary>
        /// <param name="english">In English whatever the language in use (for Claude).</param>
        public string Summary(bool english = false)
        {
            var parts = new List<Said> { Said.Raw(string.Join(", ", Creatures)) };
            if (Nest)
            {
                parts.Add(MaxNear > 0 ? Said.Of("place.spawn.upTo", MaxNear, Radius) : Said.Of("place.spawn.within", Radius));
            }
            else
            {
                var (lo, hi) = Levels;
                if (hi > 1)
                {
                    parts.Add(Said.Raw(lo == hi ? $"{lo - 1} ★" : $"{lo - 1}–{hi - 1} ★"));
                }
                parts.Add(Trigger ? Said.Of("place.spawn.wakes") : Respawns ? Said.Of("place.spawn.back", RespawnMinutes) : Said.Of("place.spawn.once"));
                if (Day != Night)
                {
                    parts.Add(Night ? Said.Of("place.spawn.nightOnly") : Said.Of("place.spawn.dayOnly"));
                }
            }
            var said = Said.Join(" · ", parts);
            return english ? said.English : said.Shown;
        }
    }
}
