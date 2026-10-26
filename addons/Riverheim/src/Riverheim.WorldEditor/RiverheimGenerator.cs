using System.Reflection;
using ValheimWorldEditor.Sdk;

namespace Riverheim.WorldEditor;

/// <summary>
/// Riverheim's world generator: its header in the world file, and the player's own Riverheim (found in the BepInEx
/// folders the editor knows, loaded with BepInEx's core and the game's Managed folder) laying the worlds out. Its
/// deprecated beta's worlds are refused with why.
/// </summary>
/// <param name="host">The editor, for the player's Riverheim and Valheim.</param>
/// <param name="id">The id: Riverheim's, or its beta's.</param>
/// <param name="log">Where the add-on's lines go.</param>
internal sealed class RiverheimGenerator(IEditorHost host, string id, Action<string> log) : IWorldGenerator
{
    /// <inheritdoc/>
    public string Id => id;

    /// <inheritdoc/>
    public string PluginId => RiverheimMod.PluginId;

    /// <inheritdoc/>
    public string Name => "Riverheim";

    /// <inheritdoc/>
    public GeneratorSettings ReadHeader(IWorldFileReader file, out int fileVersion) =>
        id == RiverheimMod.PluginId ? Settings(RiverheimMod.ReadHeader(file, out fileVersion)) : throw new InvalidDataException(Texts.Beta);

    /// <inheritdoc/>
    public void WriteHeader(IWorldFileWriter file, int fileVersion, GeneratorSettings settings) => RiverheimMod.WriteHeader(file, fileVersion, (RiverheimMod.Tag)settings.Value);

    /// <summary>
    /// Why a world with this preset cannot be drawn: Riverheim not found or not loaded, or a Riverheim that does not know
    /// the world's preset (an older one than made it); null when it can. The player's Riverheim is loaded once, then
    /// asked for each world the start page lists.
    /// </summary>
    /// <inheritdoc/>
    public string? Problem(GeneratorSettings settings)
    {
        var tag = (RiverheimMod.Tag)settings.Value;
        try
        {
            var (library, _) = Library();
            return library.Problem(tag) is string problem ? Texts.Unsupported(tag.Preset, tag.PresetVersion, library.Version, problem) : null;
        }
        catch (InvalidOperationException e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// The player's Riverheim, loaded, ready to lay out worlds with the world's preset. Throws
    /// <c>InvalidOperationException</c>, with why for the player, when it cannot be (not found, not loaded, a preset it
    /// does not know, or a live game on another release).
    /// </summary>
    /// <inheritdoc/>
    public IWorldLayout Open(GeneratorSettings settings, string? gamePluginVersion)
    {
        var tag = (RiverheimMod.Tag)settings.Value;
        var (library, dll) = Library();
        if (library.Problem(tag) is string problem)
        {
            throw new InvalidOperationException(Texts.Unsupported(tag.Preset, tag.PresetVersion, library.Version, problem));
        }
        if (gamePluginVersion != null && !RiverheimMod.SameRelease(gamePluginVersion, library.Version))
        {
            throw new InvalidOperationException(Texts.OtherVersion(gamePluginVersion, library.Version));
        }
        log($"the world's preset {tag.Preset} v{tag.PresetVersion} is laid out by Riverheim {library.Version} from {dll}.");
        return new Layout(library, tag, log);
    }

    /// <summary>
    /// The player's Riverheim's own preset, as the game makes every new world a Riverheim world while the mod is
    /// installed; none for the beta's id, or without Riverheim. Throws <c>InvalidOperationException</c> when Riverheim
    /// cannot be loaded or asked.
    /// </summary>
    /// <inheritdoc/>
    public GeneratorSettings? NewWorldSettings()
    {
        if (id != RiverheimMod.PluginId || host.FindPlugin(RiverheimMod.DllName) == null)
        {
            return null;
        }
        var (library, dll) = Library();
        try
        {
            return Settings(library.DefaultTag());
        }
        catch (Exception e) when (e is MissingMemberException or InvalidCastException or TargetInvocationException or ArgumentException)
        {
            throw new InvalidOperationException(Texts.Failed(dll), e);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Notes(GeneratorSettings settings, ExpandWorldSettings expandWorld) => RiverheimMod.Notes(expandWorld);

    /// <summary>A world's Riverheim settings as the editor keeps them, told as their preset and its version.</summary>
    internal static GeneratorSettings Settings(RiverheimMod.Tag tag) => new($"{tag.Preset} v{tag.PresetVersion}", tag);

    /// <summary>
    /// The player's Riverheim, loaded with what it names from its BepInEx folder's core and the game's Managed folder,
    /// and its file. Throws <c>InvalidOperationException</c>, with why for the player, when it is not found or cannot be
    /// loaded.
    /// </summary>
    private (RiverheimLibrary Library, string Dll) Library()
    {
        var plugin = host.FindPlugin(RiverheimMod.DllName) ?? throw new InvalidOperationException(Texts.Missing);
        var folders = new List<string> { Path.Combine(plugin.BepInExFolder, "core") };
        if (host.GameManagedFolder is { } managed)
        {
            folders.Add(managed);
        }
        try
        {
            return (RiverheimLibrary.Load(plugin.Dll, folders), plugin.Dll);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or TypeLoadException or ReflectionTypeLoadException or MissingMemberException or TargetInvocationException or InvalidOperationException or ArgumentException)
        {
            log($"loading Riverheim from {plugin.Dll} failed: {e}");
            throw new InvalidOperationException(Texts.Failed(plugin.Dll), e);
        }
    }

    /// <summary>The player's Riverheim ready to lay out worlds with a preset.</summary>
    private sealed class Layout(RiverheimLibrary library, RiverheimMod.Tag tag, Action<string> log) : IWorldLayout
    {
        /// <inheritdoc/>
        public string Version => library.Version;

        /// <inheritdoc/>
        public bool Slow => true;

        /// <summary>
        /// A world's ground, laid out by the player's Riverheim. Throws <c>InvalidOperationException</c>, with why for the
        /// player, when Riverheim fails at it.
        /// </summary>
        /// <inheritdoc/>
        public IGroundLayout LayOut(int seed)
        {
            try
            {
                return library.Ground(seed, tag);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                log($"laying out seed {seed} with Riverheim {library.Version} failed: {e}");
                throw new InvalidOperationException(Texts.LayOutFailed(library.Version, e.Message), e);
            }
        }
    }
}
