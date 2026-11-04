using DrawnWorlds;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The editor's own world generator: worlds drawn on a master map (Draw a world). Their world file starts with the
/// generator's marker and id, then the game's file version, the version of how a map becomes land, and the map
/// (<see cref="DrawnLand.Encode"/>), as the game's DrawnWorlds mod reads and writes it. Unlike an extension's generator,
/// it lays no ground out itself: the generator takes the map's continents and biomes and keeps the rest the game's
/// (<see cref="MasterMapWorlds.Rules(MasterMap, ValheimGen.WorldRules?)"/>).
/// </summary>
public sealed class DrawnWorldGenerator : IWorldGenerator
{
    /// <summary>The DrawnWorlds mod's plugin id (its BepInEx GUID).</summary>
    public const string ModId = "Tie.DrawnWorlds";

    /// <inheritdoc/>
    public string Id => MasterMapWorlds.Id;

    /// <inheritdoc/>
    public string PluginId => ModId;

    /// <inheritdoc/>
    public string Name => Lang.T("masterMap.name");

    /// <inheritdoc/>
    public GeneratorSettings ReadHeader(IWorldFileReader file, out int fileVersion)
    {
        fileVersion = file.ReadInt();
        string land = file.ReadString();
        if (land != MasterMapWorlds.LandVersion)
        {
            throw new InvalidDataException(Lang.T("masterMap.error.landVersion", land, MasterMapWorlds.LandVersion));
        }
        int length = file.ReadInt();
        if (length is < 0 or > 64 << 20)
        {
            throw new InvalidDataException(Lang.T("masterMap.error.damaged"));
        }
        byte[] kept = new byte[length];
        for (int i = 0; i < length; i++)
        {
            kept[i] = file.ReadByte();
        }
        byte[] cells = DrawnLand.Decode(kept, out int column, out int row, out ushort[]? heights);
        var map = new MasterMap(cells, column < 0 ? null : (column, row)) { Heights = heights };
        return Settings(map);
    }

    /// <inheritdoc/>
    public void WriteHeader(IWorldFileWriter file, int fileVersion, GeneratorSettings settings)
    {
        var map = (MasterMap)settings.Value;
        byte[] kept = DrawnLand.Encode(map.Cells, map.Start?.Column ?? -1, map.Start?.Row ?? -1, map.Heights);
        file.WriteInt(fileVersion);
        file.WriteString(MasterMapWorlds.LandVersion);
        file.WriteInt(kept.Length);
        foreach (byte b in kept)
        {
            file.WriteByte(b);
        }
    }

    /// <summary>A map's settings, as the world list and the generator's identity name them.</summary>
    public static GeneratorSettings Settings(MasterMap map) => new("map " + map.Stamp(), map);

    /// <inheritdoc/>
    public string? Problem(GeneratorSettings settings) => null;

    /// <summary>Never asked: a drawn world's rules come from <see cref="MasterMapWorlds"/> (ModGenerator.Rules).</summary>
    public IWorldLayout Open(GeneratorSettings settings, string? gamePluginVersion) =>
        throw new InvalidOperationException("A drawn world lays no ground out itself.");

    /// <summary>None: the New world page makes worlds from seeds; drawn worlds are made on Draw a world.</summary>
    public GeneratorSettings? NewWorldSettings() => null;

    /// <inheritdoc/>
    public IReadOnlyList<string> Notes(GeneratorSettings settings, ExpandWorldSettings expandWorld) => [];
}
