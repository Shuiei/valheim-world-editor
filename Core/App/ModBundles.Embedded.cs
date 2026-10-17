using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace TerrainEditor.App;

/// <summary>
/// Reading a mod file for bundles: a bundle file itself, or the bundles embedded in a DLL as manifest resources.
/// </summary>
public static partial class ModBundles
{
    /// <summary>The names of a DLL's manifest resources that are bundles.</summary>
    private static List<string> EmbeddedBundles(string dll)
    {
        var names = new List<string>();
        using var pe = new PEReader(File.OpenRead(dll));
        if (!pe.HasMetadata)
        {
            return names;
        }
        var md = pe.GetMetadataReader();
        foreach (var h in md.ManifestResources)
        {
            var r = md.GetManifestResource(h);
            if (r.Implementation.IsNil && Resource(pe, r, Magic.Length) is { } head && head.AsSpan().SequenceEqual(Magic))
            {
                names.Add(md.GetString(r.Name));
            }
        }
        return names;
    }

    /// <summary>
    /// An embedded resource's bytes (the first <paramref name="take"/> of them; -1: all), or null when they are not in
    /// the file.
    /// </summary>
    private static byte[]? Resource(PEReader pe, ManifestResource r, int take = -1)
    {
        var dir = pe.PEHeaders.CorHeader?.ResourcesDirectory;
        if (dir is not { Size: > 0 } d)
        {
            return null;
        }
        var block = pe.GetSectionData(d.RelativeVirtualAddress);
        if (block.Length == 0 || r.Offset < 0 || r.Offset + 4 > d.Size)
        {
            return null;
        }
        var reader = block.GetReader();
        reader.Offset = (int)r.Offset;
        int length = reader.ReadInt32();
        if (length < 0 || r.Offset + 4 + length > d.Size)
        {
            return null;
        }
        return reader.ReadBytes(take < 0 ? length : Math.Min(take, length));
    }
    /// <summary>An embedded resource of a DLL, by name.</summary>
    private static byte[]? FromDll(string dll, string resource)
    {
        using var pe = new PEReader(File.OpenRead(dll));
        var md = pe.GetMetadataReader();
        foreach (var h in md.ManifestResources)
        {
            var r = md.GetManifestResource(h);
            if (r.Implementation.IsNil && md.GetString(r.Name) == resource)
            {
                return Resource(pe, r);
            }
        }
        return null;
    }
}
