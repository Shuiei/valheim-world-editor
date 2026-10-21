using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// Riverheim, the world generation mod (by Gurebu), as the editor reads it: which worlds it made (its header in the
/// world file), whether the player's Riverheim is there to draw them (its plugin DLL in a BepInEx folder the editor
/// knows, with the Riverheim switch in Settings on: <see cref="AppSettings.Riverheim"/>), and why a world made with it
/// cannot be opened otherwise. Without its generator the ground of such a world cannot be drawn, so the editor refuses
/// it as the game does (the design: .claude/docs/riverheim-design.md).
/// </summary>
/// <remarks>Followed: Riverheim 1.2.1 (its world file header and settings, its plugin id and DLL name).</remarks>
public static class Riverheim
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
    /// Why a world cannot be opened, for the player; null when it can: a world Riverheim did not make always can; one
    /// it made needs the Riverheim switch on, the player's Riverheim found, and its generator in the editor.
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
        return find() == null ? Lang.T("riverheim.missing") : Lang.T("riverheim.notYet");
    }
}
