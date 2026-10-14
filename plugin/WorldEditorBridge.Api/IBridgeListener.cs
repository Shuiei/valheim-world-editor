namespace WorldEditorBridge.Api;

/// <summary>
/// Told what the editor changed in this game, right after it did, on the game's main thread: a mod that builds
/// things out of its objects while the game runs puts them together again here (a vehicle moved whole, a tunnel
/// dug), instead of waiting for the next load. An exception is logged and changes nothing of what the editor did.
/// </summary>
public interface IBridgeListener
{
    /// <summary>The editor's changes just applied.</summary>
    /// <param name="changes">What changed.</param>
    void OnApplied(AppliedChanges changes);
}
