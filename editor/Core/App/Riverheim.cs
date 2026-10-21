using System.Reflection;
using TerrainEditor.Save;
using ValheimGen;

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

    /// <summary>
    /// What a world file made with a world generation mod starts with where the game's file version would be (always
    /// positive), before the generator's name.
    /// </summary>
    public const int Marker = -1;

    /// <summary>The settings Riverheim keeps in a world file: the preset its world was laid out with.</summary>
    /// <param name="Preset">The preset's name ("builtin.amur" for Riverheim 1.x's own).</param>
    /// <param name="PresetVersion">The preset's version: a world keeps the one it was made with across Riverheim updates.</param>
    public sealed record Tag(string Preset, int PresetVersion);

    /// <summary>
    /// The generator's name and settings after the world file's <see cref="Marker"/>, as Riverheim writes them: the
    /// name, the game's file version, then the settings (a count, then each one's name and value; the value's type
    /// follows from the name). Throws <see cref="GeneratorHeaderException"/>, with why for the player, when the world was made
    /// with another generator or a setting the editor does not know, as the game refuses it then.
    /// </summary>
    /// <param name="pkg">The world file's package, just after the marker.</param>
    /// <param name="version">The game's file version, read between the name and the settings.</param>
    public static Tag ReadHeader(ValheimReader pkg, out int version)
    {
        string id = pkg.ReadString();
        if (id != PluginId)
        {
            throw new GeneratorHeaderException(id == BetaPluginId ? Lang.T("riverheim.beta") : Lang.T("riverheim.unknownGenerator", id));
        }
        version = pkg.ReadInt();
        string? preset = null;
        int? presetVersion = null;
        int count = pkg.ReadNumItems(WorldVersion.NumItems);
        for (int i = 0; i < count; i++)
        {
            string name = pkg.ReadString();
            switch (name)
            {
                case "preset":
                    preset = pkg.ReadString();
                    break;
                case "preset.version":
                    presetVersion = pkg.ReadInt();
                    break;
                default:
                    // The value's size depends on the name: what follows cannot be read.
                    throw new GeneratorHeaderException(Lang.T("riverheim.unknownSetting", name));
            }
        }
        return preset != null && presetVersion is int v ? new Tag(preset, v) : throw new GeneratorHeaderException(Lang.T("riverheim.noPreset"));
    }

    /// <summary>
    /// Writes Riverheim's header where the world file's version goes, as Riverheim writes it (<see cref="ReadHeader"/>
    /// reads it back): the marker, its id, the game's file version, then its settings (a one-byte count: there are
    /// two, the preset's name and version).
    /// </summary>
    /// <param name="pkg">The world file's package, at its start.</param>
    /// <param name="version">The game's file version.</param>
    /// <param name="tag">The world's preset.</param>
    public static void WriteHeader(BinaryWriter pkg, int version, Tag tag)
    {
        pkg.Write(Marker);
        pkg.Write(PluginId);
        pkg.Write(version);
        pkg.Write((byte)2);
        pkg.Write("preset");
        pkg.Write(tag.Preset);
        pkg.Write("preset.version");
        pkg.Write(tag.PresetVersion);
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
    /// Why a world cannot be opened, for the player; null when it can as far as can be told without loading Riverheim:
    /// a world Riverheim did not make always can; one it made needs the Riverheim switch on and the player's Riverheim
    /// found (<see cref="Rules"/> then loads it).
    /// </summary>
    /// <param name="tag">The world's Riverheim settings; null for a world Riverheim did not make.</param>
    /// <param name="settings">The editor's settings.</param>
    /// <param name="find">Finds the BepInEx folder with Riverheim (<see cref="Find"/>), asked only when needed.</param>
    public static string? Refusal(Tag? tag, AppSettings settings, Func<string?> find)
    {
        if (tag == null)
        {
            return null;
        }
        if (!settings.Riverheim)
        {
            return Lang.T("riverheim.off");
        }
        return find() == null ? Lang.T("riverheim.missing") : null;
    }

    /// <summary>
    /// The rules a world made with Riverheim is generated with: the player's Riverheim, loaded, laying the world out
    /// with the preset its file keeps; null for a world Riverheim did not make. Throws <c>InvalidOperationException</c>,
    /// with why for the player, when it cannot be (<see cref="Refusal"/>, a Riverheim that cannot be loaded, or one that
    /// does not know the world's preset).
    /// </summary>
    /// <param name="tag">The world's Riverheim settings; null for a world Riverheim did not make.</param>
    /// <param name="settings">The editor's settings.</param>
    /// <param name="gameVersion">The Riverheim a live game runs, when known: the player's must be of the same release
    /// (major and minor version), as Riverheim asks of the players who join a game.</param>
    public static RiverheimRules? Rules(Tag? tag, AppSettings settings, string? gameVersion = null)
    {
        string? bep = null;
        if (Refusal(tag, settings, () => bep = Find(settings)) is string why)
        {
            throw new InvalidOperationException(why);
        }
        if (tag == null || bep == null)
        {
            return null;
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
        return RulesOf(library, tag);
    }

    /// <summary>
    /// Whether two Riverheim versions lay worlds out alike: the same major and minor version (Riverheim's own rule for
    /// the players of a game); a version that cannot be read is another.
    /// </summary>
    internal static bool SameRelease(string a, string b) =>
        Version.TryParse(a, out var x) && Version.TryParse(b, out var y) && x.Major == y.Major && x.Minor == y.Minor;

    /// <summary>
    /// The rules a new world is made and looked at with (the New world page): Expand World's for a new world, with the
    /// player's Riverheim and its own preset while the Riverheim switch is on and Riverheim is found (the game makes
    /// every new world a Riverheim world while the mod is installed); without it when it cannot be loaded (said in the
    /// log).
    /// </summary>
    /// <param name="settings">The editor's settings.</param>
    public static WorldRules ForNewWorld(AppSettings settings)
    {
        var rules = ExpandWorld.ForNewWorld(settings);
        if (!settings.Riverheim || Find(settings) is not string bep)
        {
            return rules;
        }
        try
        {
            var library = Library(bep, settings);
            var tag = library.DefaultTag();
            Console.WriteLine($"Riverheim: new worlds are laid out by Riverheim {library.Version} from {bep}, preset {tag.Preset} v{tag.PresetVersion}.");
            return rules with { Riverheim = RulesOf(library, tag) };
        }
        catch (Exception e) when (e is InvalidOperationException or MissingMemberException or InvalidCastException or TargetInvocationException or ArgumentException)
        {
            Log.Caught(e, "using Riverheim for new worlds (they are made as the game makes them without it)");
            return rules;
        }
    }

    /// <summary>The rules of a world Riverheim lays out with this preset.</summary>
    private static RiverheimRules RulesOf(RiverheimLibrary library, Tag tag) =>
        new(library.Version, tag.Preset, tag.PresetVersion) { LayOut = seed => LayOut(library, seed, tag) };

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
