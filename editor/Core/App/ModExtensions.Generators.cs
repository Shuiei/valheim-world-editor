namespace TerrainEditor.App;

/// <summary>The world generators running extensions register (IWorldGenerator), for WorldGenerators.</summary>
public static partial class ModExtensions
{
    /// <summary>Whether which extensions run has been decided once (<see cref="Refresh"/>, or the tests'): until then, none run.</summary>
    internal static bool Decided => _decided;

    /// <summary>Backs <see cref="Decided"/>.</summary>
    private static volatile bool _decided;

    /// <summary>The running extensions' world generators, each turning its extension off when it faults.</summary>
    internal static List<ModGenerator> Generators =>
        [.. _state.Hooks.SelectMany(l => l.Generators.Select(g => new ModGenerator(g, e => TurnOff(l, $"{e.GetType().Name}: {e.Message}"))))];
}
