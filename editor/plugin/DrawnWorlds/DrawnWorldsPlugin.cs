using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace DrawnWorlds;

/// <summary>
/// Drawn worlds, the game's side: worlds the Valheim World Editor made from a master map (Draw a world). Such a
/// world's file carries the map; this mod reads it and writes it back on every save, and while that world runs it
/// answers the game's continents (GetBaseHeight) and biomes (GetBiome) from the map, so each biome's own shape, the
/// rivers and lakes, the vegetation and the locations are the game's own, on the drawn land. The server sends the map to
/// every player as they join; a player without the mod is turned away (their game would make another world). Worlds the
/// editor did not draw are left alone.
/// </summary>
[BepInPlugin(PluginGuid, "DrawnWorlds", Version)]
public sealed partial class DrawnWorldsPlugin : BaseUnityPlugin
{
    /// <summary>The plugin's GUID (the editor names it as the mod a drawn world needs).</summary>
    public const string PluginGuid = "Tie.DrawnWorlds";

    /// <summary>The plugin's version: the editor's.</summary>
    public const string Version = BuildInfo.Version;

    /// <summary>The generator id a drawn world's file names (after the marker -1 where the game's version goes).</summary>
    public const string GeneratorId = "vwe.mastermap";

    /// <summary>The version of how a map becomes land that this mod makes (DrawnLand).</summary>
    public const string LandVersion = "1";

    /// <summary>The plugin's log.</summary>
    internal static ManualLogSource Log { get; private set; }

    /// <summary>The maps of the worlds read from their files, by world.</summary>
    private static readonly ConditionalWeakTable<World, byte[]> Maps = new();

    /// <summary>
    /// The land of the world running, or null (a world not drawn, the main menu's): what the patched generator answers
    /// from. Set before the generator lays the world's water out (WorldGenerator.Initialize).
    /// </summary>
    internal static DrawnLand Land { get; private set; }

    /// <summary>The map <see cref="Land"/> was made of (so the same map is not laid out twice).</summary>
    private static byte[] _landOf;

    /// <summary>The start the map draws, as the zone the start temple is looked for from; null for the middle.</summary>
    internal static Vector2s? StartZone { get; private set; }

    /// <summary>Patches the game.</summary>
    private void Awake()
    {
        Log = Logger;
        new Harmony(PluginGuid).PatchAll(typeof(DrawnWorldsPlugin).Assembly);
        Logger.LogInfo($"DrawnWorlds {Version}: drawn worlds are read, played and saved as the editor drew them.");
    }

    /// <summary>The map kept with a world read from its file, or null when the world is not drawn.</summary>
    internal static byte[] MapOf(World world) => world != null && Maps.TryGetValue(world, out byte[] kept) ? kept : null;

    /// <summary>Keeps a world's map (read from its file).</summary>
    internal static void Keep(World world, byte[] kept)
    {
        Maps.Remove(world);
        Maps.Add(world, kept);
    }

    /// <summary>
    /// Makes a world's map the land the generator answers from (null: the game's own land). Laying a map out takes about
    /// a second; the same map is not laid out again.
    /// </summary>
    internal static void Use(byte[] kept, string what)
    {
        if (kept == null)
        {
            Land = null;
            _landOf = null;
            StartZone = null;
            return;
        }
        if (ReferenceEquals(kept, _landOf) && Land != null)
        {
            return;
        }
        byte[] cells = DrawnLand.Decode(kept, out int column, out int row);
        var started = DateTime.UtcNow;
        Land = new DrawnLand(cells, UnityEngine.Mathf.PerlinNoise);
        _landOf = kept;
        if (column >= 0)
        {
            DrawnLand.CenterOf(column, row, out float x, out float z);
            // The zone a point is in, as the game counts zones (64 m, centred on their middle).
            StartZone = new Vector2s((int)Math.Floor((x + 32.0) / 64.0), (int)Math.Floor((z + 32.0) / 64.0));
        }
        else
        {
            StartZone = null;
        }
        Log.LogInfo($"{what}: a drawn world, its land laid out in {(DateTime.UtcNow - started).TotalMilliseconds:0} ms" + (StartZone is { } s ? $", the start drawn in zone {s.x}, {s.y}." : ", the start at the middle."));
    }
}
