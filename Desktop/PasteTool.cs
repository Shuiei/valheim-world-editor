using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// Pasting a clip (the Paste tool): where it goes, turned (degrees, counter-clockwise seen from above)
/// and mirrored, with a height offset and repeats (like WorldEdit's //stack).
/// </summary>
public sealed partial class PasteTool
{
    /// <summary>What is pasted, null before the first copy.</summary>
    public CopyData? Clip { get; set; }
    /// <summary>The turn, in degrees, 0 to 360 (counter-clockwise seen from above).</summary>
    public float Turn { get; set; }
    /// <summary>Mirror east to west (before turning).</summary>
    public bool Mirror { get; set; }
    /// <summary>Paste the ground (shape and paint).</summary>
    public bool Ground { get; set; } = true;
    /// <summary>Paste the objects.</summary>
    public bool Objects { get; set; } = true;
    /// <summary>
    /// Clear the site: where the pasted building stands, the ground in its way is dug down to its lowest
    /// piece (a metre around it too), and the trees, rocks and the like there go (Site, for the caller).
    /// </summary>
    public bool ClearSite { get; set; } = true;
    /// <summary>The grid points the last Apply cleared (its buildings' outlines and the metre around them).</summary>
    public HashSet<(int X, int Z)> Site { get; } = [];
    /// <summary>The height added to the paste, in metres.</summary>
    public float Offset { get; set; }
    /// <summary>How many copies (see <see cref="Count"/>).</summary>
    public int Copies { get; set; } = 1;
    /// <summary>Which way the copies repeat: east along the copy's width, north along its depth, or stacked up.</summary>
    public enum Along
    {
        /// <summary>East, along the copy's width.</summary>
        Width,
        /// <summary>North, along the copy's depth.</summary>
        Depth,
        /// <summary>Stacked up.</summary>
        Up,
    }
    /// <summary>Which way the copies repeat (turned and mirrored with the paste).</summary>
    public Along Direction { get; set; } = Along.Width;
    /// <summary>The gap between copies, in metres.</summary>
    public float Gap { get; set; }
    /// <summary>Where the mouse is (grid point), for the preview.</summary>
    public Vector2? At { get; set; }
    /// <summary>A setting or the clip changed.</summary>
    public event Action? Changed;
    /// <summary>Raises <see cref="Changed"/>.</summary>
    public void Notify() => Changed?.Invoke();

    /// <summary>Turns the paste by some degrees (kept 0 to 360).</summary>
    public void TurnBy(float degrees)
    {
        Turn = ((Turn + degrees) % 360 + 360) % 360;
        Notify();
    }

    /// <summary>Source offset (from the clip's middle) → offset where it lands: mirrored, then turned.</summary>
    public Vector2 Xf(Vector2 d)
    {
        if (Mirror)
        {
            d.X = -d.X;
        }
        float t = Turn * MathF.PI / 180, c = MathF.Cos(t), s = MathF.Sin(t);
        return new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
    }

    /// <summary>
    /// A copied object's Euler angles (degrees) where it lands: mirrored east to west, which keeps the pitch
    /// (X) and reverses the yaw and the roll, then turned.
    /// </summary>
    public Vector3 Rotated(Vector3 r) => new(r.X, (Mirror ? -r.Y : r.Y) - Turn, Mirror ? -r.Z : r.Z);

    /// <summary>Offset where it lands → source offset: the inverse of <see cref="Xf"/>.</summary>
    public Vector2 Inv(Vector2 d)
    {
        float t = -Turn * MathF.PI / 180, c = MathF.Cos(t), s = MathF.Sin(t);
        d = new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
        if (Mirror)
        {
            d.X = -d.X;
        }
        return d;
    }

    /// <summary>
    /// Pieces this far (metres) below the ground they land on are out of sight on purpose (mods keep crafting
    /// stations thousands of metres down): Clear the site does not dig down to them.
    /// </summary>
    public const float HiddenDepth = 100f;
}
