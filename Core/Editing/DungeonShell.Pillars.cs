using static TerrainEditor.Editing.DungeonKit;
using System.Numerics;

namespace TerrainEditor.Editing;

internal static partial class DungeonShell
{
    /// <summary>
    /// How far past a room's top a pillar's last segment may rise: through the roof slab (2 m), not into the room
    /// above it.
    /// </summary>
    public const float RoofSlab = 2f;

    /// <summary>Whether a segment from y would rise past the roof slab over <paramref name="top"/>.</summary>
    public static bool PastRoof(Column c, float y, float top) => y + c.H > top + RoofSlab + 0.01f;

    /// <summary>
    /// A pillar or post of segments at (x, z) from y0 up to about y1 (metres): the last may rise into the roof slab
    /// over y1, never past it.
    /// </summary>
    public static void Stack(Column c, DungeonOut o, float x, float z, float y0, float y1) => Stack([c], o, x, z, y0, y1);

    /// <summary>
    /// A pillar of a material's segments at (x, z) from y0 up to about y1 (metres): at each step the first segment that
    /// fits under y1 (the list tallest first), else the last; the last may rise into the roof slab over y1, never past it.
    /// </summary>
    public static void Stack(IReadOnlyList<Column> segments, DungeonOut o, float x, float z, float y0, float y1)
    {
        float y = y0;
        while (y < y1 - 0.3f)
        {
            var c = segments.FirstOrDefault(c => c.H <= y1 - y + 0.01f) ?? segments[^1];
            if (PastRoof(c, y, y1))
            {
                break;
            }
            o.Add(c.Prefab, new Vector3(x, y + c.Bottom, z), 0);
            y += c.H;
        }
    }
}
