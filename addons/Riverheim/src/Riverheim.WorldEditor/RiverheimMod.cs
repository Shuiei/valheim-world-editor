using ValheimWorldEditor.Sdk;

namespace Riverheim.WorldEditor;

/// <summary>
/// Riverheim, the world generation mod (by Gurebu), as the add-on reads it: its ids and plugin file, the header it
/// keeps in the world files of the worlds it makes, and which of its versions lay worlds out alike.
/// </summary>
/// <remarks>Followed: Riverheim 1.2.1 (its world file header and settings, its plugin id and DLL name).</remarks>
public static partial class RiverheimMod
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
    /// with why for the player, for a setting the add-on does not know, as the game refuses it then.
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
    /// Whether two Riverheim versions lay worlds out alike: the same major and minor version (Riverheim's own rule for
    /// the players of a game); a version that cannot be read is another.
    /// </summary>
    internal static bool SameRelease(string a, string b) =>
        Version.TryParse(a, out var x) && Version.TryParse(b, out var y) && x.Major == y.Major && x.Minor == y.Minor;
}
