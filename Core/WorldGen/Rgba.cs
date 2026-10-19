namespace ValheimGen;

/// <summary>
/// A colour as Expand World Data's files give one: four 0..1 channels ("r, g, b" or "r, g, b, a", alpha 1 when left
/// out). A plain value the rules can carry and write as JSON.
/// </summary>
/// <param name="R">Red, 0..1.</param>
/// <param name="G">Green, 0..1.</param>
/// <param name="B">Blue, 0..1.</param>
/// <param name="A">Alpha, 0..1.</param>
public readonly record struct Rgba(float R, float G, float B, float A)
{
    /// <summary>The colour as the generator's mask colour.</summary>
    internal Color ToColor() => new(R, G, B, A);

    /// <summary>The colour's red, green and blue as bytes (0..255, rounded).</summary>
    public (byte R, byte G, byte B) Bytes() => (MapData.Byte(R), MapData.Byte(G), MapData.Byte(B));
}
