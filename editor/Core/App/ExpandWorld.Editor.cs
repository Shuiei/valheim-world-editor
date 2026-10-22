using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

// What the readers take from the editor: where their lines go, and the game's own rules the files are converted against.
public static partial class ExpandWorld
{
    /// <summary>Writes a line to the log.</summary>
    internal static Action<string> Note { get; set; } = Console.WriteLine;

    /// <summary>Logs an exception caught, with what was being done.</summary>
    internal static Action<Exception, string> Caught { get; set; } = (e, doing) => Log.Caught(e, doing);

    /// <summary>The game's own world rules and objects (IEditorHost.World): the location, vegetation and room files are converted against them.</summary>
    internal static IWorldGameData Game { get; set; } = GameWorldData.Instance;
}
