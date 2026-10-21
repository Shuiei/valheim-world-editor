using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The turn keys of the Place tool: a zone or box shape turns by its own rule, anything else turns the placement.
/// </summary>
public sealed partial class PlaceInput
{
    /// <summary>Turns the placement by an angle: a zone or box shape turns on its own, and says so when it cannot turn.</summary>
    /// <param name="d">The angle in degrees.</param>
    private void TurnPlacement(float d)
    {
        if (Tool.Mode is PlaceTool.Modes.Zone or PlaceTool.Modes.Grid)
        {
            if (!Tool.TurnShape(d))
            {
                Message?.Invoke(Tool.Mode == PlaceTool.Modes.Zone ? Said.Of("place.msg.turnZone") : Said.Of("place.msg.turnBox"));
            }
        }
        else
        {
            Tool.TurnBy(d);
        }
    }
}
