using HarmonyLib;

namespace DrawnWorlds;

/// <summary>
/// The generator of a drawn world: its continents and biomes from the map (the rest of the game's generator runs on
/// them unchanged), no gaps along the Ashlands' and Deep North's edges (the drawing places them), the Ashlands as
/// craggy wherever they were drawn as the game makes them along its edge, and the start temple looked for from the
/// start drawn. Each answer is the editor's (ValheimGen.WorldGenerator with a drawn land), so the ground the editor
/// shows is the ground the game makes.
/// </summary>
public sealed partial class DrawnWorldsPlugin
{
    /// <summary>The game's world radius and edge (where the ground sinks to the sea floor, then the edge's own).</summary>
    private const float WorldSize = 10000f, EdgeInner = 10490f, EdgeOfWorld = 10500f;

    /// <summary>The world's land before the generator lays its water out: the map of the world being started.</summary>
    [HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.Initialize))]
    private static class Initialize
    {
        private static void Prefix(World world)
        {
            if (world == null || world.m_menu)
            {
                Use(null, "");
                return;
            }
            // Read from the world's file (the server, or a game played alone), or sent by the server as the player joined.
            byte[] kept = MapOf(world) ?? (ZNet.instance != null && !ZNet.instance.IsServer() ? Received : null);
            try
            {
                Use(kept, world.m_name);
            }
            catch (InvalidDataException e)
            {
                // The game's own land would make another world: nothing is laid out rather than that.
                Log.LogError($"{world.m_name}: the drawn world's map cannot be read ({e.Message}).");
                throw;
            }
        }
    }

    /// <summary>No world running: no land of a map.</summary>
    [HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.Deitialize))]
    private static class Deinitialize
    {
        private static void Postfix() => Use(null, "");
    }

    /// <summary>The drawn continents inside the world, sunk past its edge as the game sinks its own.</summary>
    internal static float DrawnBaseHeight(float wx, float wy)
    {
        float h = Land.Continent(wx, wy);
        float fromMiddle = DUtils.Length(wx, wy);
        if (fromMiddle > WorldSize)
        {
            h = DUtils.Lerp(h, -0.2f, DUtils.LerpStep(WorldSize, EdgeOfWorld, fromMiddle));
            if (fromMiddle > EdgeInner)
            {
                h = DUtils.Lerp(h, -2f, Utils.LerpStep(EdgeInner, EdgeOfWorld, fromMiddle));
            }
        }
        return h * 1f + 0f;
    }

    /// <summary>The continents: the drawing's (not for the main menu's land).</summary>
    [HarmonyPatch(typeof(WorldGenerator), "GetBaseHeight")]
    private static class BaseHeight
    {
        private static bool Prefix(float wx, float wy, bool menuTerrain, ref float __result)
        {
            if (Land == null || menuTerrain)
            {
                return true;
            }
            __result = DrawnBaseHeight(wx, wy);
            return false;
        }
    }

    /// <summary>The game's biome of a kind of the map.</summary>
    internal static Heightmap.Biome BiomeOf(byte kind) => kind switch
    {
        DrawnLand.Meadows => Heightmap.Biome.Meadows,
        DrawnLand.BlackForest => Heightmap.Biome.BlackForest,
        DrawnLand.Swamp => Heightmap.Biome.Swamp,
        DrawnLand.Mountain => Heightmap.Biome.Mountain,
        DrawnLand.Plains => Heightmap.Biome.Plains,
        DrawnLand.Mistlands => Heightmap.Biome.Mistlands,
        DrawnLand.AshLands => Heightmap.Biome.AshLands,
        DrawnLand.DeepNorth => Heightmap.Biome.DeepNorth,
        _ => Heightmap.Biome.Ocean,
    };

    /// <summary>The biomes: the drawing's inside the world, the sea past its edge.</summary>
    [HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.GetBiome), [typeof(float), typeof(float), typeof(float), typeof(bool)])]
    private static class Biome
    {
        private static bool Prefix(float wx, float wy, World ___m_world, ref Heightmap.Biome __result)
        {
            if (Land == null || ___m_world.m_menu)
            {
                return true;
            }
            __result = DUtils.Length(wx, wy) > WorldSize ? Heightmap.Biome.Ocean : BiomeOf(Land.BiomeKind(wx, wy));
            return false;
        }
    }

    /// <summary>The Ashlands are where they were drawn (the boiling sea, the game's checks).</summary>
    [HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.IsAshlands))]
    private static class Ashlands
    {
        private static bool Prefix(float x, float y, ref bool __result)
        {
            if (Land == null)
            {
                return true;
            }
            __result = Land.BiomeKind(x, y) == DrawnLand.AshLands;
            return false;
        }
    }

    /// <summary>The Deep North is where it was drawn.</summary>
    [HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.IsDeepnorth))]
    private static class DeepNorth
    {
        private static bool Prefix(float x, float y, ref bool __result)
        {
            if (Land == null)
            {
                return true;
            }
            __result = Land.BiomeKind(x, y) == DrawnLand.DeepNorth;
            return false;
        }
    }

    /// <summary>No gap along the Ashlands' edge ring: a drawn world has none there.</summary>
    [HarmonyPatch(typeof(WorldGenerator), "CreateAshlandsGap")]
    private static class AshlandsGap
    {
        private static bool Prefix(ref double __result)
        {
            if (Land == null)
            {
                return true;
            }
            __result = 1.0;
            return false;
        }
    }

    /// <summary>No gap along the Deep North's edge ring.</summary>
    [HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.CreateDeepNorthGap))]
    private static class DeepNorthGap
    {
        private static bool Prefix(ref double __result)
        {
            if (Land == null)
            {
                return true;
            }
            __result = 1.0;
            return false;
        }
    }

    /// <summary>
    /// The start temple, the one kind the game lays out from the middle out, looked for from the start drawn: the same
    /// draws, around the start's zone (ZoneSystem.GetRandomZone serves only such kinds).
    /// </summary>
    [HarmonyPatch(typeof(ZoneSystem), "GetRandomZone")]
    private static class StartZoneSearch
    {
        private static bool Prefix(float range, ref Vector2s __result)
        {
            if (Land == null || StartZone is not { } start)
            {
                return true;
            }
            int n = (int)range / 64;
            Vector2s zone;
            do
            {
                zone = new Vector2s(start.x + UnityEngine.Random.Range(-n, n), start.y + UnityEngine.Random.Range(-n, n));
            }
            while (!(ZoneSystem.GetZonePos(zone).magnitude < WorldSize));
            __result = zone;
            return false;
        }
    }
}
