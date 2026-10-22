using System.Globalization;
using System.Numerics;
using NVector3 = System.Numerics.Vector3;

namespace ExpandWorld.WorldEditor;

// The terrain snapshots a PlanBuild blueprint may carry ("#TerrainHeight", "#TerrainPaint": Expand World Data's
// BlueprintTerrain): what a blueprint location's ground becomes, and how far the blueprint reaches.
public static partial class ExpandWorldMod
{
    /// <summary>The widest a snapshot may reach from the blueprint's centre (3.5 zones), as the mod allows.</summary>
    private const float MaxSnapshotReach = 64f * 3.5f;

    /// <summary>
    /// One terrain snapshot as it is read: its centre and turn in the blueprint's frame, the metres between its nodes,
    /// and its rows of nodes (a height in X, or a paint r g b a; null where a node has none).
    /// </summary>
    private sealed class Snapshot(NVector3 center, Quaternion turn, float spacing)
    {
        /// <summary>The most nodes a row, and rows, a snapshot may have; the most nodes in all.</summary>
        private const int MaxAxis = 2048, MaxCells = 1024 * 1024;

        /// <summary>Each row's nodes.</summary>
        private readonly List<Vector4?[]> _rows = [];

        /// <summary>Its centre in the blueprint's frame, metres (moved with the blueprint's centre).</summary>
        public NVector3 Center { get; private set; } = center;

        /// <summary>Its turn in the blueprint's frame (turned with the blueprint's centre piece).</summary>
        public Quaternion Turn { get; private set; } = turn;

        /// <summary>Metres between two nodes.</summary>
        public float Spacing { get; } = spacing;

        /// <summary>Whether any node has a value.</summary>
        public bool HasValues => _rows.Any(r => r.Any(v => v != null));

        /// <summary>Adds a row (BlueprintTerrain.AddRow): every row as wide as the first, within the mod's limits.</summary>
        /// <exception cref="FormatException">The row breaks a limit: the whole blueprint is not read.</exception>
        public void Add(Vector4?[] row)
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

        /// <summary>
        /// Moved with the blueprint's centre (Blueprint.Center, BlueprintTerrain.Center): a height made one above the
        /// centre (lowered by the centre's height), then its centre taken back by its own turn, and its turn taken back by
        /// the blueprint's.
        /// </summary>
        /// <param name="blueprintCenter">The blueprint's centre.</param>
        /// <param name="blueprintTurn">The turn the blueprint's parts are turned by (its centre piece's taken back).</param>
        /// <param name="lower">How much its values' X are lowered (heights: the centre's height; paints: 0).</param>
        public void MoveBy(NVector3 blueprintCenter, Quaternion blueprintTurn, float lower)
        {
            Lower(lower);
            Center -= NVector3.Transform(blueprintCenter, Turn);
            Turn = ValheimGen.TerrainSnapshot.Yaw(Turn * Quaternion.Inverse(blueprintTurn));
        }

        /// <summary>Each value's X lowered by this much (BlueprintTerrain.MapValues: heights made ones above the centre).</summary>
        private void Lower(float by)
        {
            foreach (var row in _rows)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    row[i] = row[i] is { } v ? v with { X = v.X - by } : null;
                }
            }
        }

        /// <summary>The snapshot as it is applied.</summary>
        public ValheimGen.TerrainSnapshot Read() => new(Center, Turn, Spacing, [.. _rows]);

        /// <summary>The snapshot as it is applied when a node has a value; null for one without.</summary>
        public ValheimGen.TerrainSnapshot? Values => HasValues ? Read() : null;

        /// <summary>
        /// How far its farthest node with a value is from the blueprint's centre on the ground (BlueprintTerrain.GetRadius);
        /// null when that is beyond what the mod applies (the snapshot is then dropped).
        /// </summary>
        public float? Reach()
        {
            float reach = _rows.Count == 0 ? 0f : Read().Reach();
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
    /// A snapshot's row (GetPlanBuildHeightRow, GetPlanBuildPaintRow): its nodes, a value where it is not empty: a height
    /// ("1.5", in X), or a paint ("r:g:b[:a]", a 1 when not given).
    /// </summary>
    /// <exception cref="FormatException">A value is no finite number, or a paint lacks a part: the whole blueprint is not read.</exception>
    private static Vector4?[] SnapshotRow(string row, bool paint)
    {
        string[] s = row.Replace(',', '.').Split(';');
        var values = new Vector4?[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            if (string.IsNullOrEmpty(s[i]))
            {
                continue;
            }
            values[i] = paint ? Paint(s[i]) : Height(s[i]);
        }
        return values;
    }

    /// <summary>A height node ("1.5"), in X.</summary>
    /// <exception cref="FormatException">It is no finite number.</exception>
    private static Vector4 Height(string text) =>
        Finite([text], 0, out float h) ? new Vector4(h, 0f, 0f, 0f) : throw new FormatException($"the terrain value \"{text}\" is no finite height");

    /// <summary>A paint node ("r:g:b[:a]", a 1 when not given); its r, g, b and a must be numbers, anything after them is passed over.</summary>
    /// <exception cref="FormatException">A part is missing or no finite number.</exception>
    private static Vector4 Paint(string text)
    {
        string[] parts = text.Split(':');
        float[] n = [0f, 0f, 0f, 1f];
        if (parts.Length < 3 || !Enumerable.Range(0, Math.Min(parts.Length, 4)).All(k => Finite(parts, k, out n[k])))
        {
            throw new FormatException($"the terrain value \"{text}\" is no r:g:b[:a] paint");
        }
        return new Vector4(n[0], n[1], n[2], n[3]);
    }
}
