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
    /// later), keys (32 and later), players (none). With Riverheim's settings, its header first, as Riverheim writes it:
    /// the marker, its id (another id when given), the version, then the settings (a one-byte count, each name and its
    /// value: a string or an int).
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="version">The game's file version.</param>
    /// <param name="keys">The starting global keys.</param>
    /// <param name="riverheim">Riverheim's settings, each name with its value; null for a world it did not make.</param>
    /// <param name="generator">The generator's id written after the marker (Riverheim's when null).</param>
    public static void Write(string path, int version, string[] keys, (string Name, object Value)[]? riverheim = null, string? generator = null)
    {
        using MemoryStream pkg = new();
        using (BinaryWriter p = new(pkg, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            if (riverheim != null)
            {
                p.Write(WorldGenerators.Marker);
                p.Write(generator ?? Riverheim.PluginId);
            }
            p.Write(version);
            if (riverheim != null)
            {
                p.Write((byte)riverheim.Length);
                foreach (var (name, value) in riverheim)
                {
                    p.Write(name);
                    if (value is int i)
                    {
                        p.Write(i);
                    }
                    else
                    {
                        p.Write((string)value);
                    }
                }
            }
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

    /// <summary>Riverheim 1.2.1's settings for a world made with its own preset.</summary>
    public static (string, object)[] RiverheimPreset => [("preset", "builtin.amur"), ("preset.version", 2)];
}
