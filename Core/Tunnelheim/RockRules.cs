namespace TerrainEditor.Tunnelheim;

/// <summary>What Tunnelheim's edits depend on in a game besides the rock itself.</summary>
/// <param name="MaxBuildHeight">How far above its seed ground rock may be added, in metres (the server's
/// <c>Building.MaxHeightAboveGround</c>, which a game takes from its server and the records do not keep).</param>
/// <param name="HoeOps">The game's terrain operations' settings by prefab hash, for <see cref="DigKind.Hoe"/> edits; an
/// edit naming none of them is skipped, as the mod skips it.</param>
/// <param name="DeepNorth">Whether a point (x, z) lies in the Deep North, where only a cultivator paints rock cultivated; null for nowhere.</param>
public sealed record RockRules(float MaxBuildHeight, IReadOnlyDictionary<int, HoeSettings> HoeOps, Func<float, float, bool>? DeepNorth)
{
    /// <summary>The mod's default building height, in metres.</summary>
    public const float DefaultMaxBuildHeight = 64f;

    /// <summary>The mod's default building height, no terrain operations known, no Deep North.</summary>
    public static RockRules Default { get; } = new(DefaultMaxBuildHeight, new Dictionary<int, HoeSettings>(), null);
}
