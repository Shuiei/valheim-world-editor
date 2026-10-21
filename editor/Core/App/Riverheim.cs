using System.Reflection;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// Riverheim, the world generation mod (by Gurebu), as the editor reads it: which worlds it made (its header in the
/// world file), whether the player's Riverheim is there to draw them (its plugin DLL in a BepInEx folder the editor
/// knows, with the Riverheim switch in Settings on: <see cref="AppSettings.Riverheim"/>), and why a world made with it
/// cannot be opened otherwise. Without its generator the ground of such a world cannot be drawn, so the editor refuses
/// it as the game does.
/// </summary>
/// <remarks>Followed: Riverheim 1.2.1 (its world file header and settings, its plugin id and DLL name).</remarks>
public static partial class Riverheim
{
    /// <summary>Riverheim's plugin id, which it also writes in the world file as its generator's name.</summary>
    public const string PluginId = "dev.gurebu.riverheim.stable";

    /// <summary>The id of Riverheim's deprecated beta, whose worlds no Riverheim release opens.</summary>
    public const string BetaPluginId = "dev.gurebu.riverheim.unstable";

    /// <summary>Riverheim's plugin file, under a BepInEx plugins folder.</summary>
    public const string DllName = "Riverheim.dll";

    /// <summary>The settings Riverheim keeps in a world file: the preset its world was laid out with.</summary>
    /// <param name="Preset">The preset's name ("builtin.amur" for Riverheim 1.x's own).</param>
    /// <param name="PresetVersion">The preset's version: a world keeps the one it was made with across Riverheim updates.</param>
    public sealed record Tag(string Preset, int PresetVersion);

    /// <summary>
    /// What Riverheim writes after its id in a world file: the game's file version, then its settings (a count, then
    /// each one's name and value; the value's type follows from the name). Throws <see cref="InvalidDataException"/>,
    /// with why for the player, for a setting the editor does not know, as the game refuses it then.
    /// </summary>
    /// <param name="file">The world file, just after Riverheim's id.</param>
    /// <param name="version">The game's file version, read between the id and the settings.</param>
    public static Tag ReadHeader(IWorldFileReader file, out int version)
    {
        version = file.ReadInt();
        string? preset = null;
        int? presetVersion = null;
        int count = file.ReadCount();
        for (int i = 0; i < count; i++)
        {
            string name = file.ReadString();
            switch (name)
            {
                case "preset":
                    preset = file.ReadString();
                    break;
                case "preset.version":
                    presetVersion = file.ReadInt();
                    break;
                default:
                    // The value's size depends on the name: what follows cannot be read.
                    throw new InvalidDataException(Lang.T("riverheim.unknownSetting", name));
            }
        }
        return preset != null && presetVersion is int v ? new Tag(preset, v) : throw new InvalidDataException(Lang.T("riverheim.noPreset"));
    }

    /// <summary>
    /// Writes what Riverheim writes after its id in a world file (<see cref="ReadHeader"/> reads it back): the game's
    /// file version, then its settings (a one-byte count: there are two, the preset's name and version).
    /// </summary>
    /// <param name="file">The world file, just after Riverheim's id.</param>
    /// <param name="version">The game's file version.</param>
    /// <param name="tag">The world's preset.</param>
    public static void WriteHeader(IWorldFileWriter file, int version, Tag tag)
    {
        file.WriteInt(version);
        file.WriteByte(2);
        file.WriteString("preset");
        file.WriteString(tag.Preset);
        file.WriteString("preset.version");
        file.WriteInt(tag.PresetVersion);
    }

    /// <summary>
    /// The BepInEx folder with Riverheim's plugin DLL: the folder named in Settings for Expand World
    /// (<see cref="AppSettings.ExpandWorldFolder"/>) when it has it, else the first folder the editor knows
    /// (<see cref="LocalGame.BepInExFolders"/>) that has it; null when none has.
    /// </summary>
    /// <param name="settings">The editor's settings.</param>
    public static string? Find(AppSettings settings)
    {
        var folders = LocalGame.BepInExFolders(settings).Select(f => f.BepInEx);
        if (!string.IsNullOrWhiteSpace(settings.ExpandWorldFolder))
        {
            folders = folders.Prepend(settings.ExpandWorldFolder);
        }
        return folders.FirstOrDefault(dir => ExpandWorld.HasPlugin(Path.Combine(dir, "plugins"), DllName));
    }

    /// <summary>
    /// Why a world Riverheim made cannot be opened, for the player; null when it can as far as can be told without
    /// loading Riverheim: it needs the Riverheim switch on and the player's Riverheim found (<see cref="Rules"/> then
    /// loads it).
    /// </summary>
    /// <param name="settings">The editor's settings.</param>
    /// <param name="find">Finds the BepInEx folder with Riverheim (<see cref="Find"/>), asked only when needed.</param>
    public static string? Refusal(AppSettings settings, Func<string?> find)
    {
        if (!settings.Riverheim)
        {
            return Lang.T("riverheim.off");
        }
        return find() == null ? Lang.T("riverheim.missing") : null;
    }

    /// <summary>
    /// The player's Riverheim, loaded, ready to lay out worlds with a preset. Throws <c>InvalidOperationException</c>,
    /// with why for the player, when it cannot be (<see cref="Refusal"/>, a Riverheim that cannot be loaded, or one that
    /// does not know the preset).
    /// </summary>
    /// <param name="tag">The world's Riverheim settings.</param>
    /// <param name="settings">The editor's settings.</param>
    /// <param name="gameVersion">The Riverheim a live game runs, when known: the player's must be of the same release
    /// (major and minor version), as Riverheim asks of the players who join a game.</param>
    public static IWorldLayout Rules(Tag tag, AppSettings settings, string? gameVersion = null)
    {
        string? bep = null;
        if (Refusal(settings, () => bep = Find(settings)) is string why)
        {
            throw new InvalidOperationException(why);
        }
        if (bep == null)
        {
            throw new InvalidOperationException(Lang.T("riverheim.missing"));
        }
        var library = Library(bep, settings);
        if (library.Problem(tag) is string problem)
        {
            throw new InvalidOperationException(Lang.T("riverheim.unsupported", tag.Preset, tag.PresetVersion, library.Version, problem));
        }
        if (gameVersion != null && !SameRelease(gameVersion, library.Version))
        {
            throw new InvalidOperationException(Lang.T("riverheim.otherVersion", gameVersion, library.Version));
        }
        Console.WriteLine($"Riverheim: the world's preset {tag.Preset} v{tag.PresetVersion} is laid out by Riverheim {library.Version} from {bep}.");
        return new Layout(library, tag);
    }

    /// <summary>
    /// Whether two Riverheim versions lay worlds out alike: the same major and minor version (Riverheim's own rule for
    /// the players of a game); a version that cannot be read is another.
    /// </summary>
    internal static bool SameRelease(string a, string b) =>
        Version.TryParse(a, out var x) && Version.TryParse(b, out var y) && x.Major == y.Major && x.Minor == y.Minor;

    /// <summary>
    /// The preset a new world gets: the player's Riverheim's own, while the Riverheim switch is on and Riverheim is found
    /// (the game makes every new world a Riverheim world while the mod is installed); null otherwise. Throws
    /// <c>InvalidOperationException</c> when Riverheim cannot be loaded or asked.
    /// </summary>
    /// <param name="settings">The editor's settings.</param>
    public static Tag? NewWorldTag(AppSettings settings)
    {
        if (!settings.Riverheim || Find(settings) is not string bep)
        {
            return null;
        }
        try
        {
            return Library(bep, settings).DefaultTag();
        }
        catch (Exception e) when (e is MissingMemberException or InvalidCastException or TargetInvocationException or ArgumentException)
        {
            throw new InvalidOperationException(Lang.T("riverheim.failed", bep), e);
        }
    }

    /// <summary>The player's Riverheim ready to lay out worlds with a preset (<see cref="IWorldLayout"/>).</summary>
    private sealed class Layout(RiverheimLibrary library, Tag tag) : IWorldLayout
    {
        /// <inheritdoc/>
        public string Version => library.Version;

        /// <inheritdoc/>
        public bool Slow => true;

        /// <inheritdoc/>
        public IGroundLayout LayOut(int seed) => Riverheim.LayOut(library, seed, tag);
    }

    /// <summary>
    /// The player's Riverheim in a BepInEx folder, loaded with what it names from the folder's core and the game's
    /// Managed folder. Throws <c>InvalidOperationException</c>, with why for the player, when it cannot be loaded.
    /// </summary>
    private static RiverheimLibrary Library(string bep, AppSettings settings)
    {
        string dll = ExpandWorld.FindPlugin(Path.Combine(bep, "plugins"), DllName) ?? throw new InvalidOperationException(Lang.T("riverheim.missing"));
        try
        {
            return RiverheimLibrary.Load(dll, Dependencies(bep, settings));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or TypeLoadException or ReflectionTypeLoadException or MissingMemberException or TargetInvocationException or InvalidOperationException or ArgumentException)
        {
            Log.Caught(e, $"loading Riverheim from {dll}");
            throw new InvalidOperationException(Lang.T("riverheim.failed", dll), e);
        }
    }

    /// <summary>
    /// A world's ground, laid out by the player's Riverheim (in the generator, as a world opens). Throws
    /// <c>InvalidOperationException</c>, with why for the player, when Riverheim fails at it.
    /// </summary>
    private static RiverheimGround LayOut(RiverheimLibrary library, int seed, Tag tag)
    {
        try
        {
            return library.Ground(seed, tag);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Caught(e, $"laying out seed {seed} with Riverheim {library.Version}");
            throw new InvalidOperationException(Lang.T("riverheim.layOutFailed", library.Version, e.Message), e);
        }
    }

    /// <summary>
    /// The folders the assemblies Riverheim names are taken from: the BepInEx folder's core (BepInEx, Harmony) and the
    /// game's Managed folder (Unity's), when Valheim is found.
    /// </summary>
    private static List<string> Dependencies(string bep, AppSettings settings)
    {
        var folders = new List<string> { Path.Combine(bep, "core") };
        if (GameLook.FindValheim(settings.ValheimPath) is string valheim && GameLook.DataDir(valheim) is string data)
        {
            folders.Add(Path.Combine(data, "Managed"));
        }
        return folders;
    }
}
