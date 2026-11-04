using HarmonyLib;

namespace DrawnWorlds;

/// <summary>
/// A drawn world's file: where the game's file version goes, the marker -1, the generator's id, then the game's file
/// version, the land's version and the map. The game would take -1 for a version it does not know and refuse the world
/// (as it should without this mod); here, while the game reads a world file, the first number it reads is taken past
/// the header (the version given back, the map kept with the world), and while it writes one, the header is put back
/// before the version.
/// </summary>
public sealed partial class DrawnWorldsPlugin
{
    /// <summary>The marker a world generation mod's world file has where the game's version goes.</summary>
    private const int Marker = -1;

    /// <summary>The thread reading a world file (World.LoadWorld), whose first number read is the version; 0: none.</summary>
    private static volatile int _reading;

    /// <summary>The map read from the world file being read, or null.</summary>
    [ThreadStatic]
    private static byte[] _read;

    /// <summary>The thread writing a world file (World.SaveWorldFWLData), whose first number written is the version.</summary>
    private static volatile int _writing;

    /// <summary>The map to write into the world file being written.</summary>
    private static byte[] _toWrite;

    /// <summary>The header is written: the land's version and the map go after the version.</summary>
    [ThreadStatic]
    private static bool _afterVersion;

    /// <summary>A world file is read: its first number read is the version (or a drawn world's header).</summary>
    [HarmonyPatch(typeof(World), nameof(World.LoadWorld))]
    private static class LoadWorld
    {
        private static void Prefix()
        {
            _read = null;
            _reading = Environment.CurrentManagedThreadId;
        }

        private static void Postfix(World __result)
        {
            _reading = 0;
            if (__result != null && _read != null)
            {
                Keep(__result, _read);
                Log.LogInfo($"{__result.m_name}: a drawn world ({_read.Length / 1024} KB of map).");
            }
            _read = null;
        }

        private static Exception Finalizer(Exception __exception)
        {
            _reading = 0;
            return __exception;
        }
    }

    /// <summary>The first number read of a world file: a drawn world's header taken, its version given back.</summary>
    [HarmonyPatch(typeof(ZPackage), nameof(ZPackage.ReadInt))]
    private static class ReadVersion
    {
        private static bool Prefix(ZPackage __instance, ref int __result)
        {
            if (_reading == 0 || _reading != Environment.CurrentManagedThreadId)
            {
                return true;
            }
            _reading = 0;
            int at = __instance.GetPos();
            int first = __instance.ReadInt();
            if (first != Marker)
            {
                __instance.SetPos(at);
                return true;
            }
            string id = __instance.ReadString();
            if (id != GeneratorId)
            {
                // Another world generation mod's world: its own patches read it.
                __instance.SetPos(at);
                return true;
            }
            int version = __instance.ReadInt();
            string land = __instance.ReadString();
            byte[] kept = __instance.ReadByteArray();
            if (land != LandVersion)
            {
                // Read as the game would read an unknown version: the world is refused, not drawn otherwise.
                Log.LogError($"A drawn world's land is of version {land}; this DrawnWorlds makes version {LandVersion}. Update DrawnWorlds.");
                __result = Marker;
                return false;
            }
            _read = kept;
            __result = version;
            return false;
        }
    }

    /// <summary>A world file is written: a drawn world's header goes before the version.</summary>
    [HarmonyPatch(typeof(World), nameof(World.SaveWorldFWLData), [typeof(DateTime), typeof(FileWriter)], [ArgumentType.Normal, ArgumentType.Out])]
    private static class SaveWorld
    {
        private static void Prefix(World __instance)
        {
            if (MapOf(__instance) is { } kept)
            {
                _toWrite = kept;
                _writing = Environment.CurrentManagedThreadId;
            }
        }

        private static Exception Finalizer(Exception __exception)
        {
            _writing = 0;
            _toWrite = null;
            return __exception;
        }
    }

    /// <summary>The first number written of a drawn world's file: the marker and the id before it, the map after.</summary>
    [HarmonyPatch(typeof(ZPackage), nameof(ZPackage.Write), [typeof(int)])]
    private static class WriteVersion
    {
        private static void Prefix(ZPackage __instance)
        {
            if (_writing == 0 || _writing != Environment.CurrentManagedThreadId)
            {
                return;
            }
            _writing = 0;
            __instance.Write(Marker);
            __instance.Write(GeneratorId);
            // Set once the marker is written (its own write passes through here).
            _afterVersion = true;
        }

        private static void Postfix(ZPackage __instance)
        {
            if (!_afterVersion)
            {
                return;
            }
            _afterVersion = false;
            __instance.Write(LandVersion);
            __instance.Write(_toWrite);
            _toWrite = null;
        }
    }
}
