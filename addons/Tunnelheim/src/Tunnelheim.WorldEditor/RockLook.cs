using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// How the mod paints its rock (its GroundLook): the game's terrain paint mask, picked per vertex from a small texture.
/// Most of the world uses the plain one (natural, dirt, cultivated, paved); the Deep North, the Mistlands and the
/// Ashlands use a strip of their own, two rows high, the rock's natural paint following the ground's (the Deep North's
/// snow, the others' vegetation or lava) where it was not dug.
/// </summary>
public static class RockLook
{
    /// <summary>The palettes, as the mod picks one per chunk.</summary>
    public enum Palette
    {
        /// <summary>Everywhere else: the plain 2 × 2 mask.</summary>
        Default,

        /// <summary>The Ashlands: natural rock follows the ground's lava where it lies open to the sky.</summary>
        Ashlands,

        /// <summary>The Deep North: natural rock follows the ground's snow.</summary>
        DeepNorth,

        /// <summary>The Mistlands: natural rock follows the ground's vegetation.</summary>
        Mistlands,
    }

    /// <summary>The Mistlands' biome value (Heightmap.Biome).</summary>
    private const int MistlandsBiome = 512;

    /// <summary>The game's paint mask colours (Heightmap.m_paintMask*), red in the lowest byte.</summary>
    private const uint Natural = 0xFF000000, Dirt = 0xFF0000FF, Cultivated = 0xFF00FF00, Paved = 0xFFFF0000;

    /// <summary>The plain mask, from the bottom row: natural, dirt; cultivated, paved.</summary>
    private static readonly MaskTexture Plain = new(2, 2, [Natural, Dirt, Cultivated, Paved]);

    /// <summary>
    /// The strips, from the bottom row: cultivated, natural, dirt, paved, each row's natural the ground's own (none,
    /// then snow or vegetation or lava). In the Ashlands the bottom row has no alpha (lava shows), the top row full.
    /// The Deep North's cultivated column is dirt, as the mod has it (the game does not cultivate snow).
    /// </summary>
    private static readonly MaskTexture DeepNorthStrip = new(4, 2, [Dirt & 0xFFFFFF, 0, Dirt & 0xFFFFFF, Paved & 0xFFFFFF, Dirt & 0xFFFFFF, 0x0000FF00, Dirt & 0xFFFFFF, Paved & 0xFFFFFF]);

    private static readonly MaskTexture MistlandsStrip = new(4, 2, [Cultivated, 0, Dirt & 0xFFFFFF, Paved & 0xFFFFFF, Cultivated, Natural, Dirt & 0xFFFFFF, Paved & 0xFFFFFF]);

    private static readonly MaskTexture AshlandsStrip = new(4, 2, [Cultivated & 0xFFFFFF, 0, Dirt & 0xFFFFFF, Paved & 0xFFFFFF, Cultivated, Natural, Dirt, Paved]);

    /// <summary>The palette of the chunk whose middle is a point (GroundLook.PaletteAt): the Ashlands, the Deep North, the Mistlands or the rest.</summary>
    /// <param name="area">The area, for its biomes.</param>
    /// <param name="middle">The chunk's middle.</param>
    public static Palette At(IAreaView area, Vector3 middle) =>
        area.IsAshlands(middle.X, middle.Z) ? Palette.Ashlands
        : area.IsDeepNorth(middle.X, middle.Z) ? Palette.DeepNorth
        : area.Biome(middle.X, middle.Z) == MistlandsBiome ? Palette.Mistlands
        : Palette.Default;

    /// <summary>A palette's mask texture.</summary>
    public static MaskTexture Mask(Palette palette) => palette switch
    {
        Palette.DeepNorth => DeepNorthStrip,
        Palette.Mistlands => MistlandsStrip,
        Palette.Ashlands => AshlandsStrip,
        _ => Plain,
    };

    /// <summary>
    /// The ground's own value a strip's natural row follows, from the terrain's paint mask (red in the lowest byte): its
    /// green (snow) in the Deep North, its alpha elsewhere, 0 to 1.
    /// </summary>
    public static float GroundValue(Palette palette, uint mask) =>
        (palette == Palette.DeepNorth ? (mask >> 8) & 0xFF : mask >> 24) / 255f;

    /// <summary>
    /// Where a vertex lies on its palette's mask (GroundLook.PaintUv): by its paint, unpainted rock dirt where it was dug
    /// and natural where not; on a strip, the natural row's height from the ground's value (always in the Ashlands).
    /// </summary>
    /// <param name="palette">The chunk's palette.</param>
    /// <param name="paint">The vertex's paint (<see cref="RockPaint"/>).</param>
    /// <param name="dug">Whether its cell was dug.</param>
    /// <param name="ground">The ground's value there (0 where none counts).</param>
    public static Vector2 Uv(Palette palette, byte paint, bool dug, float ground)
    {
        if (palette == Palette.Default)
        {
            return paint switch
            {
                RockPaint.Dirt => new(0.75f, 0.25f),
                RockPaint.Cultivated => new(0.25f, 0.75f),
                RockPaint.Paved => new(0.75f, 0.75f),
                RockPaint.None when dug => new(0.75f, 0.25f),
                _ => new(0.25f, 0.25f),
            };
        }
        var (column, row) = paint switch
        {
            RockPaint.Dirt => (2, 0f),
            RockPaint.Cultivated => (0, 0f),
            RockPaint.Paved => (3, 0f),
            RockPaint.Natural or RockPaint.NoVegetation => (1, 0f),
            _ when dug => (2, 0f),
            _ => (1, RockMath.Clamp01(ground)),
        };
        // In the Ashlands every paint takes its row from the ground, as the mod does: lava shows through it too.
        if (palette == Palette.Ashlands)
        {
            row = RockMath.Clamp01(ground);
        }
        return new((column + 0.5f) / 4f, (row + 0.5f) / 2f);
    }
}
