namespace TerrainEditor.Save;

/// <summary>The bridge answered a request with an error status (<see cref="LiveBridge.ApplyTerrain"/>).</summary>
/// <param name="message">What failed, with the status and the plugin's answer.</param>
/// <param name="status">The HTTP status it answered.</param>
public sealed class BridgeAnswerException(string message, int status) : InvalidOperationException(message)
{
    /// <summary>
    /// The HTTP status: the plugin answers 4xx or 503 before it does anything (token, no world loaded, not the host),
    /// 500 when what it did failed, perhaps partway.
    /// </summary>
    public int Status { get; } = status;
}
