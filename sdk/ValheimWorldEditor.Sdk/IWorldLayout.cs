namespace ValheimWorldEditor.Sdk;

/// <summary>A world generator ready to lay out worlds with one world's settings (<see cref="IWorldGenerator.Open"/>).</summary>
public interface IWorldLayout
{
    /// <summary>The version of the mod laying the worlds out ("1.2.1"): the player is told it.</summary>
    string Version { get; }

    /// <summary>Whether laying a world out takes seconds (the New world page then looks at fewer seeds at once).</summary>
    bool Slow { get; }

    /// <summary>
    /// Lays a seed's world out (as the game does when a world starts). Throws <see cref="System.InvalidOperationException"/>,
    /// with why for the player, when it cannot.
    /// </summary>
    /// <param name="seed">The world's seed.</param>
    IGroundLayout LayOut(int seed);
}
