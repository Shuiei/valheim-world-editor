
namespace ValheimGen;

// Lakes, rivers and streams, laid out once when the world is made (each from its own seed, drawn in
// the game's order) and then read by the ground: lakes where the continents dip under the water,
// rivers between lakes (not through high ground), streams on the slopes. Each is drawn into a grid of
// 64 m cells as round points along a wavy line; the ground reads how deep in a river a point is.
public sealed partial class WorldGenerator
{
    /// <summary>
    /// A river or stream: from one end to the other, its widths, and its waves. Map points: y is world z.
    /// </summary>
    public sealed class River
    {
        /// <summary>
        /// Where it starts.
        /// </summary>
        internal Vector2 P0 { get; set; }
        /// <summary>
        /// Where it ends.
        /// </summary>
        internal Vector2 P1 { get; set; }
        /// <summary>
        /// Halfway between the ends.
        /// </summary>
        internal Vector2 Center { get; set; }
        /// <summary>
        /// The narrowest a point of it is, metres (a point's radius in the ground).
        /// </summary>
        public float WidthMin { get; set; }
        /// <summary>
        /// The widest a point of it is, metres.
        /// </summary>
        public float WidthMax { get; set; }
        /// <summary>
        /// How far its waves swing sideways, metres.
        /// </summary>
        public float CurveWidth { get; set; }
        /// <summary>
        /// Its waves' length scale, metres.
        /// </summary>
        public float CurveWavelength { get; set; }
    }

    /// <summary>
    /// One point of a river's line: where, how wide (and that squared).
    /// </summary>
    /// <param name="at">Where.</param>
    /// <param name="width">How wide, metres: the ground reads it as the point's radius.</param>
    internal readonly struct RiverPoint(Vector2 at, float width) : IEquatable<RiverPoint>
    {
        /// <summary>
        /// Where.
        /// </summary>
        public readonly Vector2 At = at;
        /// <summary>
        /// How wide, metres.
        /// </summary>
        public readonly float Width = width;
        /// <summary>
        /// The width squared (rounded to float as the game's).
        /// </summary>
        public readonly float WidthSquared = (float)((double)width * width);

        /// <summary>Equal when the point, its width and its squared width are equal.</summary>
        public readonly bool Equals(RiverPoint other) => At.Equals(other.At) && Width.Equals(other.Width) && WidthSquared.Equals(other.WidthSquared);

        /// <summary>Equal to another point with the same position and widths.</summary>
        public override readonly bool Equals(object? obj) => obj is RiverPoint p && Equals(p);

        /// <summary>A hash of the position and widths, equal for equal points.</summary>
        public override readonly int GetHashCode() => HashCode.Combine(At, Width, WidthSquared);
    }

    /// <summary>
    /// Which lines Draw draws into the cells.
    /// </summary>
    private enum Streams
    {
        /// <summary>
        /// Every one (rivers).
        /// </summary>
        All,
        /// <summary>
        /// Streams starting outside the Deep North.
        /// </summary>
        NotDeepNorth,
        /// <summary>
        /// Streams starting in the Deep North.
        /// </summary>
        DeepNorthOnly,
    }

    /// <summary>
    /// The rivers' and the streams' own seeds, drawn from the world's.
    /// </summary>
    private readonly int _riverSeed, _streamSeed;

    /// <summary>
    /// The lakes' middles.
    /// </summary>
    private List<Vector2> _lakes = [];

    /// <summary>
    /// The rivers, in the order they were laid out.
    /// </summary>
    private List<River> _rivers = [];

    /// <summary>
    /// The streams as the game lists them (laid out with the Deep North not raised); those starting in the
    /// Deep North are not drawn from this list (the game keeps no list of the ones it draws there).
    /// </summary>
    private List<River> _streams = [];

    /// <summary>
    /// The river points touching each cell (read by many threads once the world is made).
    /// </summary>
    private readonly Dictionary<Vector2i, RiverPoint[]> _riverCells = [];

    /// <summary>
    /// Finds the lakes, then lays out and draws the rivers and both sets of streams, in the game's order.
    /// </summary>
    private void LayOutWater()
    {
        _lakes = FindLakes();
        _rivers = PlaceRivers();
        _streams = PlaceStreams(deepNorth: false);
        // The Deep North's streams are drawn into the ground too (the game does not keep their list).
        PlaceStreams(deepNorth: true);
    }

    /// <summary>
    /// The lakes' middles; empty for the menu world. The generator's own list: callers must not change it.
    /// </summary>
    internal List<Vector2> GetLakes() => _lakes;

    /// <summary>
    /// The rivers; the generator's own list.
    /// </summary>
    public List<River> GetRivers() => _rivers;

    /// <summary>
    /// The streams as the game lists them (those starting in the Deep North included, though not drawn from this
    /// list); the generator's own list.
    /// </summary>
    public List<River> GetStreams() => _streams;
}
