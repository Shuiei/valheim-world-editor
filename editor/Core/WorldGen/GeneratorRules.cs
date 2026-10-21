using System.Text.Json.Serialization;
using TerrainEditor.App;
using ValheimWorldEditor.Sdk;

namespace ValheimGen;

/// <summary>
/// A world made with a world generation mod (<see cref="WorldRules.Generator"/>): its generator, from an add-on or a
/// mod's extension (IWorldGenerator), lays the ground out (<see cref="LayOut"/>) in place of the game's continents,
/// biomes, rivers and lakes.
/// </summary>
/// <param name="Id">The generator's id, as the world file names it.</param>
/// <param name="Name">The mod's name ("Riverheim").</param>
/// <param name="Version">The mod's version laying the world out (another version may lay the same world out otherwise).</param>
/// <param name="Settings">The world's generator settings, as the player is told them.</param>
public sealed record GeneratorRules(string Id, string Name, string Version, string Settings)
{
    /// <summary>
    /// Lays a world out for a seed (seconds of work for some; the answer is shared by the generators of that world).
    /// Throws <c>InvalidOperationException</c>, with why for the player, when it cannot.
    /// </summary>
    [JsonIgnore]
    internal Func<int, IGroundLayout> LayOut { get; init; } = _ => throw new InvalidOperationException("No generator to lay the world out.");

    /// <summary>Whether laying a world out takes seconds (the New world page looks at fewer seeds at once).</summary>
    [JsonIgnore]
    public bool Slow { get; init; }

    /// <summary>What a new world made with these rules has its file start with (WorldCreator writes it).</summary>
    [JsonIgnore]
    public GeneratorHeader? Header { get; init; }
}
