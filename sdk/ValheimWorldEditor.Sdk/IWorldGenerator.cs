using System.Collections.Generic;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// A world generation mod's generator, for the editor to read, draw and make its worlds (register it with
/// <see cref="IExtensionRegistry.AddWorldGenerator"/>). Such a mod marks the worlds it makes: their world file starts
/// with -1 where the game's file version goes, then the generator's id (a string, as the game writes one), then what
/// the generator reads and writes here. The editor refuses a world whose generator no extension in use registers, as
/// the game without the mod refuses it.
/// </summary>
public interface IWorldGenerator
{
    /// <summary>The id the mod writes in its worlds' files, after the -1.</summary>
    string Id { get; }

    /// <summary>The mod's plugin id (its BepInEx GUID), for the version a live game runs (asked of its bridge).</summary>
    string PluginId { get; }

    /// <summary>The mod's name, as the player knows it ("Riverheim"): the world list and New world show it.</summary>
    string Name { get; }

    /// <summary>
    /// Reads what follows the id in a world file the mod made: the game's file version and the generator's settings.
    /// Throws <see cref="System.IO.InvalidDataException"/>, with why for the player, for a header it cannot read (a
    /// setting it does not know, a damaged file): the world is then not opened.
    /// </summary>
    /// <param name="file">The world file, just after the id.</param>
    /// <param name="fileVersion">The game's file version, read from the header.</param>
    GeneratorSettings ReadHeader(IWorldFileReader file, out int fileVersion);

    /// <summary>Writes what follows the id in the file of a world it makes, as <see cref="ReadHeader"/> reads it back.</summary>
    /// <param name="file">The world file, just after the id.</param>
    /// <param name="fileVersion">The game's file version.</param>
    /// <param name="settings">The world's settings.</param>
    void WriteHeader(IWorldFileWriter file, int fileVersion, GeneratorSettings settings);

    /// <summary>
    /// Why a world with these settings cannot be drawn, for the player, as far as can be told quickly (asked for every
    /// world the start page lists); null when it may be. <see cref="Open"/> tells the rest.
    /// </summary>
    /// <param name="settings">The world's settings.</param>
    string? Problem(GeneratorSettings settings);

    /// <summary>
    /// Gets ready to lay out worlds with these settings, as a world opens (or the New world page). Throws
    /// <see cref="System.InvalidOperationException"/>, with why for the player, when it cannot.
    /// </summary>
    /// <param name="settings">The world's settings.</param>
    /// <param name="gamePluginVersion">The mod's version a live game runs (its bridge's plugins list); null when not live, or unknown.</param>
    IWorldLayout Open(GeneratorSettings settings, string? gamePluginVersion);

    /// <summary>
    /// The settings a new world gets, as the game makes new worlds while the mod is installed; null when it makes none
    /// (the New world page then makes the game's own). Throws <see cref="System.InvalidOperationException"/>, with why,
    /// when it cannot tell.
    /// </summary>
    GeneratorSettings? NewWorldSettings();

    /// <summary>
    /// What Expand World's settings do otherwise to its worlds than to the game's (the editor logs each as a world opens);
    /// none when nothing.
    /// </summary>
    /// <param name="settings">The world's settings.</param>
    /// <param name="expandWorld">The world's Expand World settings.</param>
    IReadOnlyList<string> Notes(GeneratorSettings settings, ExpandWorldSettings expandWorld);
}
