using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// Riverheim's world generator (<see cref="Riverheim"/>), as a world generation mod's extension would give it: its
/// header, the switch in Settings and the player's Riverheim found for <see cref="Problem"/>, the player's Riverheim
/// laying the worlds out. Its deprecated beta's worlds are refused with why.
/// </summary>
/// <param name="id">The id: Riverheim's, or its beta's.</param>
internal sealed class RiverheimGenerator(string id) : IWorldGenerator
{
    /// <summary>Riverheim's.</summary>
    public static RiverheimGenerator Stable { get; } = new(Riverheim.PluginId);

    /// <summary>Its deprecated beta's, whose worlds no Riverheim release opens.</summary>
    public static RiverheimGenerator Beta { get; } = new(Riverheim.BetaPluginId);

    /// <inheritdoc/>
    public string Id => id;

    /// <inheritdoc/>
    public string PluginId => Riverheim.PluginId;

    /// <inheritdoc/>
    public string Name => "Riverheim";

    /// <inheritdoc/>
    public GeneratorSettings ReadHeader(IWorldFileReader file, out int fileVersion) =>
        id == Riverheim.PluginId ? Settings(Riverheim.ReadHeader(file, out fileVersion)) : throw new InvalidDataException(Lang.T("riverheim.beta"));

    /// <inheritdoc/>
    public void WriteHeader(IWorldFileWriter file, int fileVersion, GeneratorSettings settings) => Riverheim.WriteHeader(file, fileVersion, (Riverheim.Tag)settings.Value);

    /// <inheritdoc/>
    public string? Problem(GeneratorSettings settings)
    {
        var app = AppSettings.Load();
        return Riverheim.Refusal(app, () => Riverheim.Find(app));
    }

    /// <inheritdoc/>
    public IWorldLayout Open(GeneratorSettings settings, string? gamePluginVersion) => Riverheim.Rules((Riverheim.Tag)settings.Value, AppSettings.Load(), gamePluginVersion);

    /// <inheritdoc/>
    public GeneratorSettings? NewWorldSettings() => id == Riverheim.PluginId && Riverheim.NewWorldTag(AppSettings.Load()) is { } tag ? Settings(tag) : null;

    /// <inheritdoc/>
    public IReadOnlyList<string> Notes(GeneratorSettings settings, ExpandWorldSettings expandWorld) => Riverheim.Notes(expandWorld);

    /// <summary>A world's Riverheim settings as the editor keeps them, told as their preset and its version.</summary>
    internal static GeneratorSettings Settings(Riverheim.Tag tag) => new($"{tag.Preset} v{tag.PresetVersion}", tag);
}
