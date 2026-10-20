namespace ValheimGen;

/// <summary>
/// A status effect Expand World Data puts on a player in a biome or a territory (its "statusEffects"): by day, by
/// night or both (the mod's Status; neither given is both). The world and player keys that can turn it off, its
/// duration and its damage are not kept.
/// </summary>
/// <param name="Name">The status effect's name, as written.</param>
/// <param name="Day">Whether it is on by day.</param>
/// <param name="Night">Whether it is on by night.</param>
public sealed record StatusEffect(string Name, bool Day, bool Night);
