using ValheimGen;

namespace TerrainEditor.App;

/// <summary>
/// A master map: the whole world drawn as a picture, each pixel one of the legend's kinds of ground (a biome, or
/// water: river, lake, sea, deep sea), with one start point. The world the editor and the game make from it take the
/// land's height and the biomes from the picture (MasterMapLand); the game's own shapes, rivers, lakes, vegetation and
/// locations follow from there, by the game's rules. Pictures go north up, the world's 20 km square across, one cell
/// <see cref="CellMetres"/> wide.
/// </summary>
public sealed partial class MasterMap
{
    /// <summary>Cells on a side.</summary>
    public const int Size = 2048;

    /// <summary>Half the square the picture covers, metres: the game's world radius.</summary>
    public const float Half = 10000f;

    /// <summary>Metres a cell (an exact binary fraction: 20000 / 2048).</summary>
    public const float CellMetres = 2 * Half / Size;

    /// <summary>The kind of ground a cell is.</summary>
    public enum Kind : byte
    {
        /// <summary>Meadows.</summary>
        Meadows,
        /// <summary>Black Forest.</summary>
        BlackForest,
        /// <summary>Swamp.</summary>
        Swamp,
        /// <summary>Mountain.</summary>
        Mountain,
        /// <summary>Plains.</summary>
        Plains,
        /// <summary>Mistlands.</summary>
        Mistlands,
        /// <summary>Ashlands.</summary>
        AshLands,
        /// <summary>Deep North.</summary>
        DeepNorth,
        /// <summary>A river: a channel below the water line, in the biome of its banks.</summary>
        River,
        /// <summary>A lake: shallow water inland, in the biome of its shores.</summary>
        Lake,
        /// <summary>The sea, shallow along the coast.</summary>
        Sea,
        /// <summary>The deep sea.</summary>
        DeepSea,
    }

    /// <summary>A kind of the legend: its colour in the picture, its name's text key, and the game's biome it makes.</summary>
    /// <param name="Kind">The kind.</param>
    /// <param name="R">Red.</param>
    /// <param name="G">Green.</param>
    /// <param name="B">Blue.</param>
    /// <param name="Key">The text key of its name.</param>
    /// <param name="Biome">The game's biome; None for a river or a lake (the biome of its banks).</param>
    public sealed record Entry(Kind Kind, byte R, byte G, byte B, string Key, Heightmap.Biome Biome)
    {
        /// <summary>The colour as players type it ("#92A75C").</summary>
        public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>
    /// The legend, in <see cref="Kind"/> order: the biomes in the game's map colours (so a drawing looks like the in-game
    /// map), the Mountain off-white so that the Deep North keeps the white, and the water in blues, lighter as it is
    /// shallower.
    /// </summary>
    public static readonly IReadOnlyList<Entry> Legend =
    [
        new(Kind.Meadows, 0x92, 0xA7, 0x5C, "biome.meadows", Heightmap.Biome.Meadows),
        new(Kind.BlackForest, 0x6B, 0x74, 0x3F, "biome.blackForest", Heightmap.Biome.BlackForest),
        new(Kind.Swamp, 0xA3, 0x72, 0x58, "biome.swamp", Heightmap.Biome.Swamp),
        new(Kind.Mountain, 0xE0, 0xE0, 0xD6, "biome.mountain", Heightmap.Biome.Mountain),
        new(Kind.Plains, 0xE7, 0xAB, 0x78, "biome.plains", Heightmap.Biome.Plains),
        new(Kind.Mistlands, 0x33, 0x33, 0x33, "biome.mistlands", Heightmap.Biome.Mistlands),
        new(Kind.AshLands, 0x7B, 0x20, 0x20, "biome.ashlands", Heightmap.Biome.AshLands),
        new(Kind.DeepNorth, 0xFF, 0xFF, 0xFF, "biome.deepNorth", Heightmap.Biome.DeepNorth),
        new(Kind.River, 0x7C, 0xC4, 0xF0, "masterMap.kind.river", Heightmap.Biome.None),
        new(Kind.Lake, 0x3C, 0x8D, 0xD6, "masterMap.kind.lake", Heightmap.Biome.None),
        new(Kind.Sea, 0x1F, 0x5F, 0xA8, "masterMap.kind.sea", Heightmap.Biome.Ocean),
        new(Kind.DeepSea, 0x0B, 0x2A, 0x5B, "masterMap.kind.deepSea", Heightmap.Biome.Ocean),
    ];

    /// <summary>The start point's colour (magenta, far from every colour of the legend).</summary>
    public static readonly (byte R, byte G, byte B) StartColour = (0xFF, 0x00, 0xFF);

    /// <summary>Whether a kind is water (river, lake, sea or deep sea).</summary>
    public static bool IsWater(Kind k) => k >= Kind.River;

    /// <summary>The cells, <see cref="Size"/> × <see cref="Size"/>, row by row from the north, west first.</summary>
    public byte[] Cells { get; }

    /// <summary>The start point's cell (column, row), or null: the middle of the world.</summary>
    public (int Column, int Row)? Start { get; set; }

    /// <summary>A map of one kind everywhere (a blank sea to draw land on).</summary>
    /// <param name="fill">The kind.</param>
    public MasterMap(Kind fill = Kind.DeepSea)
    {
        Cells = new byte[Size * Size];
        Array.Fill(Cells, (byte)fill);
    }

    /// <summary>A map of these cells (taken, not copied).</summary>
    /// <exception cref="ArgumentException">Not <see cref="Size"/> × <see cref="Size"/> cells, or a cell that is no kind.</exception>
    public MasterMap(byte[] cells, (int Column, int Row)? start)
    {
        if (cells.Length != Size * Size)
        {
            throw new ArgumentException($"a master map has {Size} × {Size} cells", nameof(cells));
        }
        if (Array.FindIndex(cells, c => c >= Legend.Count) is int bad and >= 0)
        {
            throw new ArgumentException($"cell {bad} is no kind of the legend", nameof(cells));
        }
        Cells = cells;
        Start = start;
    }

    /// <summary>A copy (for the painter's undo).</summary>
    public MasterMap Copy() => new((byte[])Cells.Clone(), Start);

    /// <summary>The kind at a cell.</summary>
    public Kind At(int column, int row) => (Kind)Cells[row * Size + column];

    /// <summary>The cell a world point is in, held to the picture.</summary>
    /// <param name="x">World x (east).</param>
    /// <param name="z">World z (north).</param>
    public static (int Column, int Row) CellOf(float x, float z) =>
        (Math.Clamp((int)Math.Floor((x + Half) / CellMetres), 0, Size - 1), Math.Clamp((int)Math.Floor((Half - z) / CellMetres), 0, Size - 1));

    /// <summary>The world point at a cell's middle.</summary>
    public static (float X, float Z) CenterOf(int column, int row) => (-Half + (column + 0.5f) * CellMetres, Half - (row + 0.5f) * CellMetres);

    /// <summary>The start point in the world: its cell's middle, or the middle of the world (0, 0) when none is drawn.</summary>
    public (float X, float Z) StartPoint => Start is { } s ? CenterOf(s.Column, s.Row) : (0f, 0f);

    /// <summary>Paints a disc of cells.</summary>
    /// <param name="column">The disc's middle column.</param>
    /// <param name="row">Its middle row.</param>
    /// <param name="radius">Its radius in cells (0: the one cell).</param>
    /// <param name="kind">The kind painted.</param>
    /// <returns>The cells changed, as a rectangle (column, row, width, height), or null when none was.</returns>
    public (int X, int Y, int W, int H)? Disc(int column, int row, float radius, Kind kind)
    {
        int r = (int)Math.Ceiling(radius);
        int x0 = Math.Max(0, column - r), x1 = Math.Min(Size - 1, column + r);
        int y0 = Math.Max(0, row - r), y1 = Math.Min(Size - 1, row + r);
        float r2 = (radius + 0.5f) * (radius + 0.5f);
        bool changed = false;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int dx = x - column, dy = y - row;
                if (dx * dx + dy * dy <= r2 && Cells[y * Size + x] != (byte)kind)
                {
                    Cells[y * Size + x] = (byte)kind;
                    changed = true;
                }
            }
        }
        return changed ? (x0, y0, x1 - x0 + 1, y1 - y0 + 1) : null;
    }

    /// <summary>
    /// Fills the connected cells of the kind at a cell (sideways and up and down, not diagonally) with another kind.
    /// </summary>
    /// <returns>The cells changed, as a rectangle, or null when none was (the cell is of that kind already).</returns>
    public (int X, int Y, int W, int H)? Fill(int column, int row, Kind kind)
    {
        byte from = Cells[row * Size + column], to = (byte)kind;
        if (from == to)
        {
            return null;
        }
        int x0 = column, x1 = column, y0 = row, y1 = row;
        var todo = new Stack<int>();
        todo.Push(row * Size + column);
        Cells[row * Size + column] = to;
        while (todo.Count > 0)
        {
            int i = todo.Pop();
            int x = i % Size, y = i / Size;
            x0 = Math.Min(x0, x);
            x1 = Math.Max(x1, x);
            y0 = Math.Min(y0, y);
            y1 = Math.Max(y1, y);
            if (x > 0 && Cells[i - 1] == from) { Cells[i - 1] = to; todo.Push(i - 1); }
            if (x < Size - 1 && Cells[i + 1] == from) { Cells[i + 1] = to; todo.Push(i + 1); }
            if (y > 0 && Cells[i - Size] == from) { Cells[i - Size] = to; todo.Push(i - Size); }
            if (y < Size - 1 && Cells[i + Size] == from) { Cells[i + Size] = to; todo.Push(i + Size); }
        }
        return (x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    /// <summary>How many cells each kind has, in <see cref="Kind"/> order.</summary>
    public int[] Counts()
    {
        int[] n = new int[Legend.Count];
        foreach (byte c in Cells)
        {
            n[c]++;
        }
        return n;
    }

    /// <summary>
    /// A short stamp of the cells and the start (FNV-1a, 64 bits, as hex): two maps with the same stamp make the same
    /// world, so the generator's identity carries it.
    /// </summary>
    public string Stamp()
    {
        ulong h = 14695981039346656037UL;
        foreach (byte c in Cells)
        {
            h = (h ^ c) * 1099511628211UL;
        }
        if (Start is { } s)
        {
            h = (h ^ (ulong)s.Column) * 1099511628211UL;
            h = (h ^ (ulong)s.Row) * 1099511628211UL;
        }
        return h.ToString("x16");
    }
}
