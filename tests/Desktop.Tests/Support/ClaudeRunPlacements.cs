namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Placing pieces through the Claude tools of a <see cref="ClaudeRun"/>, for the tests that need an object to act on.
/// </summary>
internal static class ClaudeRunPlacements
{
    /// <summary>
    /// Places one piece unsnapped at this x (z 0) in the open area, and returns its id.
    /// </summary>
    /// <param name="r">The run whose tools place the piece.</param>
    /// <param name="prefab">The piece's prefab name.</param>
    /// <param name="x">The piece's x, in metres from the area's middle.</param>
    /// <returns>The id of the placed object.</returns>
    internal static async Task<int> PlaceUnsnapped(this ClaudeRun r, string prefab, float x) =>
        ClaudeRun.J(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = prefab, X = x, Z = 0, Snap = false }]))
            .GetProperty("placed")[0].GetProperty("id").GetInt32();
}
