using System.Numerics;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// The surface of one chunk's rock, as Tunnelheim meshes it (Surface Nets: one vertex in each cell the surface passes
/// through, quads across the edges where the density changes sign), positions relative to the chunk's corner, normals
/// pointing into the air, triangles wound as Unity draws them (clockwise seen from the front).
/// </summary>
/// <param name="Positions">The vertices, in metres from the chunk's lowest corner.</param>
/// <param name="Normals">Each vertex's normal (unit length, into the air).</param>
/// <param name="Paint">Each vertex's paint: the most common one among its cell's corners (<see cref="RockPaint"/>).</param>
/// <param name="Dug">Whether each vertex's cell differs from the seed ground (the mod draws such rock as dirt when unpainted).</param>
/// <param name="Triangles">Three vertex indices a triangle.</param>
public sealed record RockMesh(Vector3[] Positions, Vector3[] Normals, byte[] Paint, bool[] Dug, int[] Triangles)
{
    /// <summary>An empty mesh.</summary>
    public static RockMesh Empty { get; } = new([], [], [], [], []);
}

/// <summary>Meshes a <see cref="RockGrid"/>'s chunks (<see cref="RockMesh"/>).</summary>
/// <remarks>Followed: Tunnelheim 1.0.0 (SurfaceNets.Prepare, Compute, FillPoints, AddQuad, AddTriangle; VoxelWorld.GatherPadded).</remarks>
public static class RockMesher
{
    private const int N = RockGrid.Samples;

    /// <summary>Points a side of the padded block: the chunk's samples and one more on each side, plus the next chunk's first.</summary>
    private const int M = N + 3;

    /// <summary>Cells a side of the padded block.</summary>
    private const int C = N + 2;

    private static readonly int[] Corner = [0, 1, M, M + 1, M * M, M * M + 1, M * M + M, M * M + M + 1];

    private static readonly (int A, int B)[] Edges =
    [
        (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7),
    ];

    private static readonly Vector3 NearlyDown = new(0.001f, -0.9999995f, 0f);

    /// <summary>The surface of a chunk; empty when there is no such chunk or nothing crosses it.</summary>
    /// <param name="grid">The rock.</param>
    /// <param name="cx">The chunk's column index along x.</param>
    /// <param name="cy">Its height index.</param>
    /// <param name="cz">Its column index along z.</param>
    public static RockMesh Build(RockGrid grid, int cx, int cy, int cz)
    {
        if (grid.Chunk(cx, cy, cz) == null)
        {
            return RockMesh.Empty;
        }
        var block = Padded.Of(grid, cx, cy, cz);
        var mesh = new Builder(block);
        mesh.Vertices();
        int hiX = grid.IsColumn(cx + 1, cz) ? N : N + 1;
        int hiZ = grid.IsColumn(cx, cz + 1) ? N : N + 1;
        mesh.Quads(hiX, hiZ);
        return mesh.Result();
    }

    private static int PointIndex(int x, int y, int z) => x + 1 + M * (y + 1 + M * (z + 1));

    private static int CellIndex(int x, int y, int z) => x + 1 + C * (y + 1 + C * (z + 1));

    /// <summary>
    /// The densities, seed densities and paints of a chunk's padded block (local points -1 to 33): a chunk that is
    /// there gives its stored values (seed ground rounded as stored where it keeps none); elsewhere the seed ground,
    /// not rounded.
    /// </summary>
    private sealed class Padded
    {
        public readonly float[] Density = new float[M * M * M];
        public readonly float[] Seed = new float[M * M * M];
        public readonly byte[] Paint = new byte[M * M * M];

        public static Padded Of(RockGrid grid, int cx, int cy, int cz)
        {
            var b = new Padded();
            float[] heights = new float[M * M];
            for (int k = -1; k <= N + 1; k++)
            {
                for (int l = -1; l <= N + 1; l++)
                {
                    heights[l + 1 + M * (k + 1)] = grid.SeedAt(cx * N + l, cz * N + k);
                }
            }
            var near = new RockChunk?[27];
            for (int m = -1; m <= 1; m++)
            {
                for (int n = -1; n <= 1; n++)
                {
                    for (int o = -1; o <= 1; o++)
                    {
                        near[o + 1 + 3 * (n + 1 + 3 * (m + 1))] = grid.Chunk(cx + o, cy + n, cz + m);
                    }
                }
            }
            for (int i = -1; i <= N + 1; i++)
            {
                for (int j = -1; j <= N + 1; j++)
                {
                    float y = (cy * N + j) * RockGrid.Step;
                    for (int k = -1; k <= N + 1; k++)
                    {
                        b.Fill(near, heights, i, j, k, y);
                    }
                }
            }
            return b;
        }

        private void Fill(RockChunk?[] near, float[] heights, int i, int j, int k, float y)
        {
            int at = k + 1 + M * (j + 1 + M * (i + 1));
            var chunk = near[Part(k) + 3 * (Part(j) + 3 * Part(i))];
            float ground = heights[k + 1 + M * (i + 1)] - y;
            if (chunk == null)
            {
                Density[at] = ground;
                Seed[at] = ground;
                return;
            }
            int inChunk = Local(k) + N * (Local(j) + N * Local(i));
            float seed = RockChunk.Quantize(ground);
            Density[at] = chunk.IsStored ? chunk.Raw(inChunk) * (1f / 256f) : seed;
            Paint[at] = chunk.PaintAt(inChunk);
            Seed[at] = seed;
        }

        private static int Part(int v) => v < 0 ? 0 : v < N ? 1 : 2;

        private static int Local(int v) => v < 0 ? v + N : v >= N ? v - N : v;
    }

    /// <summary>Gathers the vertices, then the quads, of one padded block.</summary>
    private sealed class Builder(Padded block)
    {
        private readonly int[] _cellVertex = new int[C * C * C];
        private readonly List<Vector3> _positions = [];
        private readonly List<Vector3> _normals = [];
        private readonly List<byte> _paint = [];
        private readonly List<bool> _dug = [];
        private readonly List<int> _triangles = [];

        public void Vertices()
        {
            for (int i = -1; i <= N; i++)
            {
                for (int j = -1; j <= N; j++)
                {
                    for (int k = -1; k <= N; k++)
                    {
                        _cellVertex[CellIndex(k, j, i)] = Vertex(k, j, i);
                    }
                }
            }
        }

        /// <summary>The vertex of a cell the surface crosses (its index), or -1.</summary>
        private int Vertex(int x, int y, int z)
        {
            int p = PointIndex(x, y, z);
            int inside = 0;
            foreach (int o in Corner)
            {
                if (block.Density[p + o] > 0f)
                {
                    inside++;
                }
            }
            if (inside is 0 or 8)
            {
                return -1;
            }
            float[] c = new float[8];
            int[] votes = new int[256];
            bool dug = false;
            for (int m = 0; m < 8; m++)
            {
                int at = p + Corner[m];
                c[m] = block.Density[at];
                votes[block.Paint[at]]++;
                dug |= Math.Abs(c[m] - block.Seed[at]) > 0.001f;
            }
            _positions.Add((new Vector3(x, y, z) + Crossing(c)) * RockGrid.Step);
            _normals.Add(Normal(c));
            _paint.Add(MostPainted(votes));
            _dug.Add(dug);
            return _positions.Count - 1;
        }

        /// <summary>The mean of the points where the cell's edges cross the surface, in cell units.</summary>
        private static Vector3 Crossing(float[] c)
        {
            var sum = Vector3.Zero;
            int count = 0;
            foreach (var (a, b) in Edges)
            {
                if (c[a] > 0f != c[b] > 0f)
                {
                    float t = (float)(c[a] / ((double)c[a] - c[b]));
                    var pa = new Vector3(a & 1, (a >> 1) & 1, (a >> 2) & 1);
                    var pb = new Vector3(b & 1, (b >> 1) & 1, (b >> 2) & 1);
                    sum += pa + (pb - pa) * t;
                    count++;
                }
            }
            return new Vector3(sum.X / count, sum.Y / count, sum.Z / count);
        }

        /// <summary>The cell's normal, from its corners' differences, pointing into the air.</summary>
        private static Vector3 Normal(float[] c)
        {
            var g = new Vector3(
                (float)((double)c[1] + c[3] + c[5] + c[7] - ((double)c[0] + c[2] + c[4] + c[6])),
                (float)((double)c[2] + c[3] + c[6] + c[7] - ((double)c[0] + c[1] + c[4] + c[5])),
                (float)((double)c[4] + c[5] + c[6] + c[7] - ((double)c[0] + c[1] + c[2] + c[3])));
            var n = RockMath.LengthSquared(g) > 1E-08f ? -RockMath.Normalize(g) : Vector3.UnitY;
#pragma warning disable S1244 // The mod tests for an exactly vertical normal.
            return n.X == 0f && n.Z == 0f && n.Y < 0f ? NearlyDown : n;
#pragma warning restore S1244
        }

        private static byte MostPainted(int[] votes)
        {
            byte best = 0;
            int most = 0;
            for (int b = 1; b < votes.Length; b++)
            {
                if (votes[b] > most)
                {
                    most = votes[b];
                    best = (byte)b;
                }
            }
            return best;
        }

        public void Quads(int hiX, int hiZ)
        {
            for (int z = 0; z < hiZ; z++)
            {
                for (int y = 0; y < N; y++)
                {
                    for (int x = 0; x < hiX; x++)
                    {
                        EdgeQuads(x, y, z);
                    }
                }
            }
        }

        private void EdgeQuads(int x, int y, int z)
        {
            bool solid = block.Density[PointIndex(x, y, z)] > 0f;
            if (solid != block.Density[PointIndex(x + 1, y, z)] > 0f)
            {
                Quad(CellIndex(x, y - 1, z - 1), CellIndex(x, y, z - 1), CellIndex(x, y, z), CellIndex(x, y - 1, z), !solid);
            }
            if (solid != block.Density[PointIndex(x, y + 1, z)] > 0f)
            {
                Quad(CellIndex(x - 1, y, z - 1), CellIndex(x, y, z - 1), CellIndex(x, y, z), CellIndex(x - 1, y, z), solid);
            }
            if (solid != block.Density[PointIndex(x, y, z + 1)] > 0f)
            {
                Quad(CellIndex(x - 1, y - 1, z), CellIndex(x, y - 1, z), CellIndex(x, y, z), CellIndex(x - 1, y, z), !solid);
            }
        }

        private void Quad(int ca, int cb, int cc, int cd, bool flip)
        {
            int a = _cellVertex[ca], b = _cellVertex[cb], c = _cellVertex[cc], d = _cellVertex[cd];
            if (a < 0 || b < 0 || c < 0 || d < 0)
            {
                return;
            }
            if (flip)
            {
                (b, d) = (d, b);
            }
            if (RockMath.LengthSquared(_positions[a] - _positions[c]) <= RockMath.LengthSquared(_positions[b] - _positions[d]))
            {
                Triangle(a, b, c);
                Triangle(a, c, d);
            }
            else
            {
                Triangle(a, b, d);
                Triangle(b, c, d);
            }
        }

        private void Triangle(int a, int b, int c)
        {
            var cross = RockMath.Cross(_positions[b] - _positions[a], _positions[c] - _positions[a]);
            if (!(RockMath.LengthSquared(cross) < 1E-10f))
            {
                _triangles.Add(a);
                _triangles.Add(b);
                _triangles.Add(c);
            }
        }

        public RockMesh Result() => new([.. _positions], [.. _normals], [.. _paint], [.. _dug], [.. _triangles]);
    }
}
