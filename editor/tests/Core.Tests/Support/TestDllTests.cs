using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The tests' DLL writer (TestDll) read back as a reader of .NET metadata reads it: its name, its manifest resources
/// with their bytes, its string literals.
/// </summary>
public sealed class TestDllTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-testdll-" + Guid.NewGuid().ToString("N")[..8]);

    public TestDllTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Fact]
    public void TheAssemblyHoldsItsResourcesAndStrings()
    {
        string path = Path.Combine(_dir, "Mod.dll");

        TestDll.Write(path, [("a.bundle", [1, 2, 3]), ("b.txt", "b"u8.ToArray())], ["MBParentId", "hello world"]);

        using var pe = new PEReader(File.OpenRead(path));
        var md = pe.GetMetadataReader();
        Assert.Equal("Mod", md.GetString(md.GetAssemblyDefinition().Name));
        var dir = pe.PEHeaders.CorHeader!.ResourcesDirectory;
        var block = pe.GetSectionData(dir.RelativeVirtualAddress);
        var found = md.ManifestResources.Select(md.GetManifestResource).Select(r =>
        {
            var reader = block.GetReader();
            reader.Offset = (int)r.Offset;
            return (md.GetString(r.Name), reader.ReadBytes(reader.ReadInt32()));
        }).ToList();
        Assert.Equal(["a.bundle", "b.txt"], found.Select(f => f.Item1));
        Assert.Equal(new byte[] { 1, 2, 3 }, found[0].Item2);
        Assert.Equal("b"u8.ToArray(), found[1].Item2);
        var strings = new List<string>();
        for (var h = MetadataTokens.UserStringHandle(1); !h.IsNil; h = md.GetNextHandle(h))
        {
            // The heap ends with padding, read as an empty string.
            if (md.GetUserString(h) is { Length: > 0 } s)
            {
                strings.Add(s);
            }
        }
        Assert.Equal(["MBParentId", "hello world"], strings);
    }
}
