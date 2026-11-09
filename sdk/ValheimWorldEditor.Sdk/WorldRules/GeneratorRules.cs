using System;
using System.Text.Json.Serialization;
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

    /// <summary>
    /// The land a player drew (the editor's master map), or null. When set, the generator takes only the continents and
    /// the biomes from it and keeps everything else the game's: <see cref="LayOut"/> is not asked, and the game's water
    /// is laid out on that land.
    /// </summary>
    [JsonIgnore]
    internal IDrawnLand? Land { get; init; }

    /// <summary>
    /// Where the player drew the start (with <see cref="Land"/>; the middle, 0, 0, by default): the start temple is
    /// looked for from there out, as the game looks for it from the middle out.
    /// </summary>
    [JsonIgnore]
    internal (float X, float Z) Start { get; init; }

    /// <summary>Whether laying a world out takes seconds (the New world page looks at fewer seeds at once).</summary>
    [JsonIgnore]
    public bool Slow { get; init; }

    /// <summary>
    /// What a new world made with these rules has its file start with (the editor's TerrainEditor.App.GeneratorHeader:
    /// the generator and the world's settings; WorldCreator writes it).
    /// </summary>
    [JsonIgnore]
    internal object? Header { get; init; }
}
