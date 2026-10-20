using System.Globalization;
using System.Numerics;
using NVector3 = System.Numerics.Vector3;

namespace TerrainEditor.App;

// The terrain snapshots a PlanBuild blueprint may carry ("#TerrainHeight", "#TerrainPaint": Expand World Data's
// BlueprintTerrain), read for what they change about the blueprint: its radius, and whether it levels its ground itself.
public static partial class ExpandWorld
{
    /// <summary>The widest a snapshot may reach from the blueprint's centre (3.5 zones), as the mod allows.</summary>
    private const float MaxSnapshotReach = 64f * 3.5f;

    /// <summary>
    /// One terrain snapshot: its centre and turn in the blueprint's frame, the metres between its nodes, and which
    /// nodes of its rows have a value (the values themselves are checked, not kept).
    /// </summary>
    private sealed class Snapshot(NVector3 center, Quaternion turn, float spacing)
    {
        /// <summary>The most nodes a row, and rows, a snapshot may have; the most nodes in all.</summary>
        private const int MaxAxis = 2048, MaxCells = 1024 * 1024;

        /// <summary>Each row's nodes: whether it has a value.</summary>
        private readonly List<bool[]> _rows = [];

        /// <summary>Its centre in the blueprint's frame, metres (moved with the blueprint's centre).</summary>
        public NVector3 Center { get; private set; } = center;

        /// <summary>Metres between two nodes.</summary>
        public float Spacing { get; } = spacing;

        /// <summary>Whether any node has a value.</summary>
        public bool HasValues => _rows.Any(r => r.Any(v => v));

        /// <summary>Adds a row (BlueprintTerrain.AddRow): every row as wide as the first, within the mod's limits.</summary>
        /// <exception cref="FormatException">The row breaks a limit: the whole blueprint is not read.</exception>
        public void Add(bool[] row)
        {
            if (row.Length is 0 or > MaxAxis || _rows.Count >= MaxAxis || (long)row.Length * (_rows.Count + 1) > MaxCells)
            {
                throw new FormatException($"a terrain row of {row.Length} nodes, after {_rows.Count} rows: more than a snapshot holds");
            }
            if (_rows.Count > 0 && row.Length != _rows[0].Length)
            {
                throw new FormatException($"a terrain row of {row.Length} nodes, the first had {_rows[0].Length}");
            }
            _rows.Add(row);
        }

        /// <summary>Moved with the blueprint's centre (BlueprintTerrain.Center): its centre taken back by its own turn.</summary>
        public void MoveBy(NVector3 blueprintCenter) => Center -= NVector3.Transform(blueprintCenter, turn);

        /// <summary>
        /// How far its farthest node with a value is from the blueprint's centre on the ground (BlueprintTerrain.GetRadius);
        /// null when that is beyond what the mod applies (the snapshot is then dropped).
        /// </summary>
        public float? Reach()
        {
            if (_rows.Count == 0)
            {
                return 0f;
            }
            float s = MathF.Max(0.001f, Spacing), x0 = Center.X - (_rows[0].Length - 1) * s * 0.5f, z0 = Center.Z - (_rows.Count - 1) * s * 0.5f, reach = 0f;
            for (int z = 0; z < _rows.Count; z++)
            {
                for (int x = 0; x < _rows[z].Length; x++)
                {
                    if (_rows[z][x])
                    {
                        float nx = x0 + x * s, nz = z0 + z * s;
                        reach = MathF.Max(reach, (float)Math.Sqrt((double)nx * nx + (double)nz * nz));
                    }
                }
            }
            bool finite = float.IsFinite(Center.X) && float.IsFinite(Center.Y) && float.IsFinite(Center.Z) && float.IsFinite(reach);
            return finite && reach + MathF.Max(1f, Spacing) <= MaxSnapshotReach ? reach : null;
        }
    }

    /// <summary>
    /// A snapshot's header ("#TerrainHeight:x,z,y;yaw;spacing", BlueprintTerrain's GetPlanBuildTerrain); null when it is
    /// not one the mod reads (the section is then passed over).
    /// </summary>
    private static Snapshot? SnapshotOf(string row, int colon)
    {
        string[] s = colon < 0 ? [] : row[(colon + 1)..].Split(';');
        string[] c = s.Length >= 3 ? s[0].Split(',') : [];
        if (c.Length != 3 || !Finite(c, 0, out float x) || !Finite(c, 1, out float z) || !Finite(c, 2, out float y) || !Finite(s, 1, out float yaw) || !Finite(s, 2, out float spacing) || spacing <= 0f)
        {
            return null;
        }
        double half = yaw * Math.PI / 180.0 * 0.5;
        return new Snapshot(new NVector3(x, y, z), new Quaternion(0f, (float)Math.Sin(half), 0f, (float)Math.Cos(half)), spacing);
    }

    /// <summary>A required finite number of a row (RequiredInvariantFloat); false when it is missing, no number, or not finite.</summary>
    private static bool Finite(string[] s, int index, out float value)
    {
        value = 0f;
        return index < s.Length && !string.IsNullOrWhiteSpace(s[index]) && float.TryParse(s[index], NumberStyles.Any, NumberFormatInfo.InvariantInfo, out value) && float.IsFinite(value);
    }

    /// <summary>
    /// A snapshot's row: its nodes, with a value where it is not empty: a height ("1.5"), or a paint ("r:g:b[:a]").
    /// </summary>
    /// <exception cref="FormatException">A value is no finite number, or a paint lacks a part: the whole blueprint is not read.</exception>
    private static bool[] SnapshotRow(string row, bool paint)
    {
        string[] s = row.Replace(',', '.').Split(';');
        bool[] has = new bool[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            if (string.IsNullOrEmpty(s[i]))
            {
                continue;
            }
            string[] parts = paint ? s[i].Split(':') : [s[i]];
            // A paint's r, g, b and a (when given) must be numbers; anything after them is passed over.
            if (parts.Length < (paint ? 3 : 1) || !Enumerable.Range(0, Math.Min(parts.Length, 4)).All(k => Finite(parts, k, out _)))
            {
                throw new FormatException($"the terrain value \"{s[i]}\" is no {(paint ? "r:g:b[:a] paint" : "finite height")}");
            }
            has[i] = true;
        }
        return has;
    }
}
