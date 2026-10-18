namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The editor with a made-up Valheim whose terrain shader does not compile (a line of its fragment program cut short):
/// the game look cannot be set up on the graphics card, which the editor says, and the area draws in plain colours.
/// </summary>
public sealed class BrokenLookEditorProcess() : GameLookEditorProcess(Fragment.Replace("SV_Target1 = vec4(0.04, 0.04, 0.04, 0.2);", "SV_Target1 = vec4(0.04, 0.04, 0.04, 0.2", StringComparison.Ordinal))
{
}
