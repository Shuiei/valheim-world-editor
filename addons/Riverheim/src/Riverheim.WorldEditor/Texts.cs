namespace Riverheim.WorldEditor;

/// <summary>What the add-on tells the player (the editor shows it on the world's card, or as the world fails to open).</summary>
internal static class Texts
{
    public const string Missing = "Made with Riverheim, which is in none of the BepInEx folders the editor knows: install it, or add its BepInEx folder in Settings.";

    public const string Beta = "Made with Riverheim's beta: its worlds cannot be opened, not even by Riverheim 1.0 and later.";

    public const string NoPreset = "Its Riverheim settings have no preset: the world file is damaged.";

    public static string UnknownSetting(string name) => $"Made with a Riverheim setting the editor does not know ({name}): a newer add-on may know it.";

    public static string Unsupported(string preset, int version, string riverheim, string words) =>
        $"Made with Riverheim's preset {preset} v{version}, which your Riverheim {riverheim} does not know ({words}): update Riverheim.";

    public static string Failed(string dll) => $"Riverheim could not be loaded from {dll}: see the log.";

    public static string LayOutFailed(string riverheim, string why) => $"Your Riverheim {riverheim} could not lay this world out: {why}";

    public static string OtherVersion(string game, string yours) =>
        $"The game runs Riverheim {game} and yours is {yours}: the editor needs the same release (the first two numbers), as players who join the game do. Install Riverheim {game}.";
}
