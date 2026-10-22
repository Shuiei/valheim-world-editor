using TerrainEditor.App;

namespace ExpandWorld.WorldEditor.Tests;

/// <summary>The readers converting against the editor's own game rules (as the editor hands them to the add-on), from the tests' fixtures.</summary>
internal static class EditorGame
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Use()
    {
        ExpandWorldMod.Game = GameWorldData.Instance;
        ExpandWorldMod.Note = Console.WriteLine;
    }
}
