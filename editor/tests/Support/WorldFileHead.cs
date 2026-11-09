using TerrainEditor.App;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>A world file's head (_main.&lt;n&gt;.fwl2) written as the game writes it, for the tests to read back.</summary>
public static class WorldFileHead
{
    /// <summary>The test world's name, seed and generator version in the head written.</summary>
    public const string Name = "CITest", SeedName = "GbXfypqU3G";

    /// <summary>
    /// Writes a world file as the game does: the version, names, seed, uid, generator version, database flag (30 and
    /// later), keys (32 and later), players (none). With a generator's id, a world generation mod's marker and that id
    /// first (nothing more of a header: for a generator none draws).
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="version">The game's file version.</param>
    /// <param name="keys">The starting global keys.</param>
    /// <param name="generator">The generator's id written after the marker; null for a world the game made.</param>
    public static void Write(string path, int version, string[] keys, string? generator = null) =>
        Write(path, version, keys, p =>
        {
            if (generator != null)
            {
                p.Write(WorldGenerators.Marker);
                p.Write(generator);
            }
            p.Write(version);
        });

    /// <summary>
    /// Writes a world file as the game does with Worldhalla's mark: the mark with its format's version, the game's file
    /// version, then Worldhalla's part (the generator's id, Worldhalla's own settings, the generator's settings).
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="generator">Worldhalla's id for the generator.</param>
    /// <param name="settings">The generator's settings, as it keeps them.</param>
    /// <param name="own">Worldhalla's own settings (none in Worldhalla 1.0).</param>
    /// <param name="format">The version of Worldhalla's format (0 in Worldhalla 1.0).</param>
    public static void WriteWorldhalla(string path, string generator, byte[] settings, byte[]? own = null, int format = 0) =>
        Write(path, 42, [], p =>
        {
            p.Write(WorldGenerators.WorldhallaFlag | (uint)format);
            p.Write(42);
            using MemoryStream part = new();
            using (BinaryWriter w = new(part, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                w.Write(generator);
                w.Write((own ?? []).Length);
                w.Write(own ?? []);
                w.Write(settings.Length);
                w.Write(settings);
            }
            p.Write((int)part.Length);
            p.Write(part.ToArray());
        });

    /// <summary>Writes a world file as the game does, its head (up to the file version) written by <paramref name="head"/>.</summary>
    private static void Write(string path, int version, string[] keys, Action<BinaryWriter> head)
    {
        using MemoryStream pkg = new();
        using (BinaryWriter p = new(pkg, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            head(p);
            p.Write(Name);
            p.Write(SeedName);
            p.Write(StableHash.Of(SeedName));
            p.Write(123456789L);
            p.Write(2);
            if (version >= 30)
            {
                p.Write(true);
            }
            if (version >= 32)
            {
                p.Write(keys.Length);
                foreach (string k in keys)
                {
                    p.Write(k);
                }
            }
            p.Write(0);
        }
        File.WriteAllBytes(path, [.. BitConverter.GetBytes((int)pkg.Length), .. pkg.ToArray()]);
    }
}
