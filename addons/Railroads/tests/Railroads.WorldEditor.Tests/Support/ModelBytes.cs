using System.Numerics;
using System.Text;

namespace Railroads.WorldEditor.Tests;

/// <summary>Model files made for a test, in the mod's form (ModelFile), and a stand-in for the mod's folder holding them.</summary>
internal static class ModelBytes
{
    /// <summary>A model file of parts (each its slot, corners and triangles), lines and axles; boxes and snap points given in number only.</summary>
    public static byte[] Write(IReadOnlyList<(string Slot, Vector3[] Corners, int[] Triangles)> parts, IReadOnlyList<Vector3[]>? lines = null,
        IReadOnlyList<Vector3>? axles = null, int boxes = 1, int snaps = 2)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        w.Write(Encoding.ASCII.GetBytes(ModelFile.Magic));
        w.Write(parts.Count);
        foreach (var (slot, corners, triangles) in parts)
        {
            byte[] name = Encoding.UTF8.GetBytes(slot);
            w.Write(name.Length);
            w.Write(name);
            w.Write(corners.Length);
            foreach (var c in corners)
            {
                // Position, then a normal and a texture place the add-on reads past.
                foreach (float f in new[] { c.X, c.Y, c.Z, 0f, 1f, 0f, 0.25f, 0.75f })
                {
                    w.Write(f);
                }
            }
            w.Write(triangles.Length);
            foreach (int i in triangles)
            {
                w.Write(i);
            }
        }
        w.Write(boxes);
        for (int i = 0; i < boxes * 12; i++)
        {
            w.Write(0.5f);
        }
        w.Write(snaps);
        for (int i = 0; i < snaps * 3; i++)
        {
            w.Write(1f);
        }
        w.Write(lines?.Count ?? 0);
        foreach (var line in lines ?? [])
        {
            w.Write(line.Length);
            foreach (var p in line)
            {
                w.Write(p.X);
                w.Write(p.Y);
                w.Write(p.Z);
            }
        }
        w.Write(axles?.Count ?? 0);
        foreach (var a in axles ?? [])
        {
            w.Write(a.X);
            w.Write(a.Y);
            w.Write(a.Z);
        }
        w.Flush();
        return stream.ToArray();
    }

    /// <summary>A model of one triangle in one slot, its first corner at <paramref name="at"/>.</summary>
    public static byte[] Triangle(string slot, Vector3 at, IReadOnlyList<Vector3>? axles = null) =>
        Write([(slot, [at, at + Vector3.UnitZ, at + Vector3.UnitX], [0, 1, 2])], axles: axles);
}

/// <summary>A stand-in for the mod's folder in a BepInEx folder of the test's own: its plugin file and its models; deleted when disposed.</summary>
internal sealed class ModFolder : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "railroads-addon-" + Guid.NewGuid().ToString("N")[..8]);

    public ModFolder()
    {
        Directory.CreateDirectory(Meshes);
        File.WriteAllBytes(Plugin, [0]);
    }

    /// <summary>The mod's plugin file.</summary>
    public string Plugin => Path.Combine(_dir, "BepInEx", "plugins", "VladasZ-Railroads", RailroadsMod.DllName);

    /// <summary>The folder of its models.</summary>
    public string Meshes => Path.Combine(Path.GetDirectoryName(Plugin)!, RailroadsMod.MeshFolder);

    /// <summary>Puts a model file in its folder.</summary>
    public void Put(string model, byte[] bytes) => File.WriteAllBytes(Path.Combine(Meshes, model + ".bfm"), bytes);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }
}
