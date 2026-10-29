
namespace TerrainEditor.Desktop;

/// <summary>One stroke of a brush: from the button going down to it coming up.</summary>
public sealed class Stroke
{
    /// <summary>The tool of the stroke.</summary>
    public required BrushTool Tool { get; init; }
    /// <summary>The ground as it was when the stroke started.</summary>
    public required Ground.State Start { get; init; }
    /// <summary>The grid points the stroke changed.</summary>
    public HashSet<int> Touched { get; } = [];
    /// <summary>Naturalize: each point's aim, worked out once per stroke.</summary>
    public Dictionary<int, float> Natural { get; } = [];
    /// <summary>
    /// Flatten: the height taken from the ground where the stroke started (Target from click), or null for the brush's.
    /// </summary>
    public float? Target { get; set; }
    /// <summary>The game's ±8 m limit stopped the ground somewhere.</summary>
    public bool Clamped { get; set; }

    /// <summary>A point's height when the stroke started.</summary>
    public float StartHeight(Ground g, int p) => g.Base[p] + Start.Lift[p] + (Start.Mod[p] != 0
        ? Math.Clamp(Start.Level[p] + Start.Smooth[p], -Editing.EditStore.MaxLevel, Editing.EditStore.MaxLevel)
        : 0);
}
