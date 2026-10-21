using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace WorldEditor.Tests;

/// <summary>
/// A .NET assembly with no code, as a mod's DLL is read by the editor (as bytes, never loaded): manifest resources (a
/// mod's embedded bundles) and string literals (the user strings a mod's code holds, such as its data names).
/// </summary>
public static class TestDll
{
    /// <summary>
    /// Writes the assembly, named after the file, holding these resources and these string literals.
    /// </summary>
    /// <param name="path">The DLL's path.</param>
    /// <param name="resources">Manifest resources: name and bytes.</param>
    /// <param name="strings">String literals (user strings).</param>
    public static void Write(string path, IEnumerable<(string Name, byte[] Bytes)>? resources = null, IEnumerable<string>? strings = null)
    {
        var md = new MetadataBuilder();
        string name = Path.GetFileNameWithoutExtension(path);
        md.AddModule(0, md.GetOrAddString(name + ".dll"), md.GetOrAddGuid(Guid.NewGuid()), default, default);
        md.AddAssembly(md.GetOrAddString(name), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        md.AddTypeDefinition(default, default, md.GetOrAddString("<Module>"), default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        foreach (string s in strings ?? [])
        {
            md.GetOrAddUserString(s);
        }
        var blob = new BlobBuilder();
        foreach (var (resource, bytes) in resources ?? [])
        {
            md.AddManifestResource(ManifestResourceAttributes.Public, md.GetOrAddString(resource), default, (uint)blob.Count);
            blob.WriteInt32(bytes.Length);
            blob.WriteBytes(bytes);
            blob.Align(8);
        }
        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(md), new BlobBuilder(), managedResources: blob);
        var image = new BlobBuilder();
        pe.Serialize(image);
        File.WriteAllBytes(path, image.ToArray());
    }
}
