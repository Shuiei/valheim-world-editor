using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaTool
{
    // ---- Ground actions.
    /// <summary>The actions that change the ground inside the selection.</summary>
    public enum GroundAction
    {
        /// <summary>Towards one height.</summary>
        Flatten,
        /// <summary>Up by an amount.</summary>
        Raise,
        /// <summary>Down by an amount.</summary>
        Lower,
        /// <summary>Bumps evened out.</summary>
        Smooth,
        /// <summary>Natural-looking bumps (Naturalize).</summary>
        Natural,
        /// <summary>Back to the generated ground, paint removed.</summary>
        Restore,
        /// <summary>Weathered: slopes settle and rain cuts gullies.</summary>
        Erode,
        /// <summary>Painted with a ground colour.</summary>
        Paint,
    }

    /// <summary>An action's name as shown to the player, or in English for Claude.</summary>
    /// <param name="a">The action.</param>
    /// <param name="english">In English whatever the language in use (for Claude).</param>
    public static string Label(GroundAction a, bool english = false) => english ? Named(a).English : Named(a).Shown;

    /// <summary>An action's name in both languages.</summary>
    /// <param name="a">The action.</param>
    public static Said Named(GroundAction a)
    {
        return a switch
        {
            GroundAction.Flatten => Said.Of("brush.label.flatten"),
            GroundAction.Raise => Said.Of("brush.label.raise"),
            GroundAction.Lower => Said.Of("brush.label.lower"),
            GroundAction.Smooth => Said.Of("brush.label.smooth"),
            GroundAction.Natural => Said.Of("brush.label.natural"),
            GroundAction.Restore => Said.Of("area.action.restore"),
            GroundAction.Erode => Said.Of("brush.label.erode"),
            _ => Said.Of("common.paint"),
        };
    }
}
