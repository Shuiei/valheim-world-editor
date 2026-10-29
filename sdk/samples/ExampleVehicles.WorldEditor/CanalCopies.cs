using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ExampleVehicles.WorldEditor;

/// <summary>
/// Canals go with the editor's copies (the area copier hook): a canal whose whole path lies in what is copied is kept
/// as offsets from the copy's middle, and dug again where the copy is pasted, turned and mirrored with it. Its object
/// only holds the mod's data, which no copy takes by itself.
/// </summary>
public sealed class CanalCopies : IAreaCopier
{
    /// <summary>The form of what is kept: this version byte, the canals' count, then each canal (see <see cref="Write"/>).</summary>
    private const byte Version = 1;

    /// <inheritdoc/>
    public byte[]? Copy(IAreaView area, CopyRegion region)
    {
        var canals = new List<(float Width, float Depth, List<Vector2> Offsets)>();
        foreach (var canal in area.ObjectsOf(Keys.Canal))
        {
            if (canal.TryGetBytes(Keys.Path, out byte[] bytes) && Canals.ReadPath(bytes) is { Count: >= 2 } path
                && path.TrueForAll(p => region.Contains(p.X, p.Y)))
            {
                canal.TryGetFloat(Keys.Width, out float width);
                canal.TryGetFloat(Keys.Depth, out float depth);
                canals.Add((width, depth, path.ConvertAll(p => new Vector2(p.X - region.Middle.X, p.Y - region.Middle.Z))));
            }
        }
        return canals.Count == 0 ? null : Write(canals);
    }

    /// <inheritdoc/>
    public ToolEdit? Paste(IAreaView area, byte[] kept, IReadOnlyList<CopyPlacement> copies)
    {
        if (Read(kept) is not { } canals)
        {
            return null;
        }
        var edit = new ToolEdit();
        foreach (var copy in copies)
        {
            foreach (var (width, depth, offsets) in canals)
            {
                var path = offsets.ConvertAll(o => Flat(copy.Place(new Vector3(o.X, 0, o.Y))));
                var first = path[0];
                edit.Add(Keys.Canal, new Vector3(first.X, area.GroundHeight(first.X, first.Y), first.Y), Vector3.Zero, c =>
                {
                    c.SetBytes(Keys.Path, Canals.WritePath(path));
                    // A canal keeping no width or depth (0 here) has the mod's own.
                    c.SetFloat(Keys.Width, width > 0 ? width : null);
                    c.SetFloat(Keys.Depth, depth > 0 ? depth : null);
                });
            }
        }
        return edit;
    }

    /// <summary>A world point's x and z.</summary>
    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);

    /// <summary>Canals in the kept form: for each, its width and depth, its points' count and each offset (x, z).</summary>
    private static byte[] Write(List<(float Width, float Depth, List<Vector2> Offsets)> canals)
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream))
        {
            w.Write(Version);
            w.Write(canals.Count);
            foreach (var (width, depth, offsets) in canals)
            {
                w.Write(width);
                w.Write(depth);
                w.Write(offsets.Count);
                foreach (var o in offsets)
                {
                    w.Write(o.X);
                    w.Write(o.Y);
                }
            }
        }
        return stream.ToArray();
    }

    /// <summary>Canals read back from the kept form; null when the bytes are not in it (cut short, or another version).</summary>
    private static List<(float Width, float Depth, List<Vector2> Offsets)>? Read(byte[] kept)
    {
        using var r = new BinaryReader(new MemoryStream(kept));
        bool Left(long bytes) => r.BaseStream.Length - r.BaseStream.Position >= bytes;
        if (!Left(5) || r.ReadByte() != Version)
        {
            return null;
        }
        var canals = new List<(float, float, List<Vector2>)>();
        for (int i = r.ReadInt32(); i > 0; i--)
        {
            if (!Left(12))
            {
                return null;
            }
            float width = r.ReadSingle(), depth = r.ReadSingle();
            int n = r.ReadInt32();
            if (n < 2 || !Left(n * 8L))
            {
                return null;
            }
            var offsets = new List<Vector2>(n);
            for (int k = 0; k < n; k++)
            {
                offsets.Add(new Vector2(r.ReadSingle(), r.ReadSingle()));
            }
            canals.Add((width, depth, offsets));
        }
        return canals;
    }
}
