using ValheimWorldEditor.Sdk;

namespace ExpandWorld.WorldEditor;

// What the readers take from the editor: where their lines go, and the game's own rules the files are converted against.
public static partial class ExpandWorldMod
{
    /// <summary>Writes a line to the editor's log (the extension's IExtensionRegistry.Log once registered).</summary>
    internal static Action<string> Note { get; set; } = Console.WriteLine;

    /// <summary>Logs an exception caught, with what was being done (in one line: its type and message).</summary>
    internal static Action<Exception, string> Caught { get; set; } = (e, doing) => Note($"{doing}: {e.GetType().Name}: {e.Message}");

    /// <summary>
    /// The game's own world rules and objects (IEditorHost.World): the location, vegetation and room files are converted
    /// against them. Set as the extension registers (the tests set their own) and before a world's rules are asked.
    /// </summary>
    internal static IWorldGameData Game { get; set; } = null!;
}
