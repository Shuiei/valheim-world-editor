namespace ValheimGen;

/// <summary>The rules' colours (<see cref="Rgba"/>) as the generator and the map use them.</summary>
public static class RgbaColors
{
    /// <summary>The colour as the generator's mask colour.</summary>
    /// <param name="c">The colour.</param>
    internal static Color ToColor(this Rgba c) => new(c.R, c.G, c.B, c.A);

    /// <summary>The colour's red, green and blue as bytes (0..255, rounded).</summary>
    /// <param name="c">The colour.</param>
    public static (byte R, byte G, byte B) Bytes(this Rgba c) => (MapData.Byte(c.R), MapData.Byte(c.G), MapData.Byte(c.B));
}
