namespace TerrainEditor.App;

/// <summary>
/// A world file made with a world generation mod whose header the editor cannot read (another generator, a setting it
/// does not know): the world cannot be opened, and the message says why, for the player.
/// </summary>
/// <param name="message">Why, for the player.</param>
public sealed class GeneratorHeaderException(string message) : IOException(message);
