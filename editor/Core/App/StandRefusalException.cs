namespace TerrainEditor.App;

/// <summary>
/// A stand does not take an item (<see cref="StandData.ForItemStand"/>, <see cref="StandData.ForArmourStand"/>): the
/// message is the reason in English (what Claude is told), <see cref="Reason"/> the same in the language in use too.
/// </summary>
/// <param name="reason">Why the stand does not take it, in both languages.</param>
public sealed class StandRefusalException(Said reason) : ArgumentException(reason.English)
{
    /// <summary>Why the stand does not take it, in both languages (the inspector shows the user's).</summary>
    public Said Reason { get; } = reason;
}
