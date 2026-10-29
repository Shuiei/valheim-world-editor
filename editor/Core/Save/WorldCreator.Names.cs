using TerrainEditor.App;

namespace TerrainEditor.Save;

public static partial class WorldCreator
{
    /// <summary>The game's seed number for a seed name (World: m_seed = m_seedName.GetStableHashCode()).</summary>
    public static int SeedOf(string seedName) => StableHash.Of(seedName);

    /// <summary>
    /// After the random events' state, what the game's save holds: an empty list, brotli-compressed JSON {"list":[]}
    /// behind its length (copied from a world the game made).
    /// </summary>
    private static readonly byte[] EmptyListBlock = [0x0f, 0, 0, 0, 0x0b, 0x05, 0x80, (byte)'{', (byte)'"', (byte)'l', (byte)'i', (byte)'s', (byte)'t', (byte)'"', (byte)':', (byte)'[', (byte)']', (byte)'}', 0x03];

    /// <summary>
    /// The characters no file name may hold on Windows (Linux and macOS forbid only / and NUL, but a world is copied
    /// between systems).
    /// </summary>
    private static readonly char[] ForbiddenInNames = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// Whether a world name can be a file name on every system the world may be copied to, the same on each of
    /// them: not empty, none of / \ : * ? " &lt; &gt; | or a control character, not ending with a dot or a space, and
    /// not one of Windows' device names (CON, NUL, CONIN$, COM1…, COM¹…) with or without an extension. The game names
    /// the world's files after it.
    /// </summary>
    /// <param name="name">The name, as typed (not trimmed).</param>
    public static bool IsWorldName(string name)
    {
        if (name.Length == 0 || name[^1] is '.' or ' ' || name.Any(c => c < ' ' || c == '\x7f' || ForbiddenInNames.Contains(c)))
        {
            return false;
        }
        return !SafeFile.IsDeviceName(name);
    }
}
