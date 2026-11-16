using Avalonia.Controls;

namespace TerrainEditor.Desktop;

// The settings the Area tool's actions share with the brushes and the Path tool, shown with the action that uses them.
public sealed partial class AreaPanel
{
    /// <summary>Naturalize: another random pattern for the bumps (the brush's, as the Naturalize brush's New pattern).</summary>
    internal Button NewPatternButton { get; }

    /// <summary>Naturalize: the brush's Bumps, Bump size and New pattern (as the Naturalize brush shows them).</summary>
    internal Control NaturalRows { get; }

    /// <summary>Erode: the brush's rest angle, in degrees (as the Erode brush shows it).</summary>
    internal Slider RestSlider { get; }

    /// <summary>Erode: the rest angle's row.</summary>
    internal Control RestRow { get; }
}
