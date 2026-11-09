using ValheimWorldEditor.Sdk;

namespace Riverheim.WorldEditor;

/// <summary>
/// Riverheim, the world generation mod (by Gurebu), as the add-on reads it: its ids and plugin file, the header it
/// keeps in the world files of the worlds it makes, and which of its versions lay worlds out alike.
/// </summary>
/// <remarks>
/// Followed: Riverheim 1.3.1 (its world file header and settings, its plugin id and DLL name), with Worldhalla 1.0.0,
/// the framework it is a plugin of since 1.3.0: Worldhalla keeps the world's settings in a header of its own, under
/// <see cref="WorldhallaId"/> (the editor reads that header, <see cref="ReadHeader"/> the settings in it).
/// </remarks>
public static partial class RiverheimMod
{
    /// <summary>
    /// Riverheim's plugin id, which Riverheim before 1.3 also writes in the world file as its generator's name (and
    /// Worldhalla still reads there, for the worlds those versions made).
    /// </summary>
    public const string PluginId = "dev.gurebu.riverheim.stable";

    /// <summary>
    /// The editor's id for the worlds Worldhalla keeps with Riverheim's generator (Riverheim 1.3 and later, which make
    /// every world so and turn an older world into one on its first save): Worldhalla's prefix, then the id Riverheim
    /// gives its generator in Worldhalla.
    /// </summary>
    public const string WorldhallaId = "worldhalla:dev.gurebu.riverheim";

    /// <summary>The id of Riverheim's deprecated beta, whose worlds no Riverheim release opens.</summary>
    public const string BetaPluginId = "dev.gurebu.riverheim.unstable";

    /// <summary>Riverheim's plugin file, under a BepInEx plugins folder.</summary>
    public const string DllName = "Riverheim.dll";

    /// <summary>Worldhalla's plugin file, beside Riverheim's (Riverheim 1.3 and later need it).</summary>
    public const string WorldhallaDllName = "Worldhalla.dll";

    /// <summary>The settings Riverheim keeps in a world file: the preset its world was laid out with.</summary>
    /// <param name="Preset">The preset's name ("builtin.amur" for Riverheim 1.x's own).</param>
    /// <param name="PresetVersion">The preset's version: a world keeps the one it was made with across Riverheim updates.</param>
    public sealed record Tag(string Preset, int PresetVersion);

    /// <summary>
    /// What Riverheim before 1.3 writes after its id in a world file: the game's file version, then its settings (a
    /// count, then each one's name and value; the value's type follows from the name). A world Worldhalla keeps is read
    /// the same way: the editor gives the game's file version, then the settings Riverheim 1.3 and later keep in
    /// Worldhalla's header, laid out alike. Throws <see cref="InvalidDataException"/>, with why for the player, for a
    /// setting the add-on does not know, as the game refuses it then.
    /// </summary>
    /// <param name="file">The world file, just after Riverheim's id (or the settings Worldhalla keeps).</param>
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
                    throw new InvalidDataException(Texts.UnknownSetting(name));
            }
        }
        return preset != null && presetVersion is int v ? new Tag(preset, v) : throw new InvalidDataException(Texts.NoPreset);
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
    /// Whether two Riverheim versions lay worlds out alike, by the mod's own rule for the players of a game: before 1.3,
    /// the same major and minor version; from 1.3 (through Worldhalla, which compares the generator's version as a
    /// whole), the same version. A version that cannot be read is another.
    /// </summary>
    internal static bool SameRelease(string a, string b) =>
        Version.TryParse(a, out var x) && Version.TryParse(b, out var y) && x.Major == y.Major && x.Minor == y.Minor
        && (!ThroughWorldhalla(x) || x.Build == y.Build);

    /// <summary>Whether a Riverheim version is a Worldhalla plugin (1.3 and later).</summary>
    private static bool ThroughWorldhalla(Version v) => v.Major > 1 || (v.Major == 1 && v.Minor >= 3);
}
