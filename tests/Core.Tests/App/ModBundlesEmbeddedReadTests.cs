using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading a mod DLL's embedded bundles: only the resources that hold a Unity bundle (their bytes start with the bundle
/// header) are listed, and a named resource is read back as its exact bytes.
/// </summary>
public sealed class ModBundlesEmbeddedReadTests : AppSettingsTestBase
{
    /// <summary>The embedded bundle names of a DLL.</summary>
    private static List<string> EmbeddedBundles(string dll) => (List<string>)PrivateCall.Static(typeof(ModBundles), "EmbeddedBundles", dll)!;

    /// <summary>The bytes of a named embedded resource, or null.</summary>
    private static byte[]? FromDll(string dll, string resource) => (byte[]?)PrivateCall.Static(typeof(ModBundles), "FromDll", dll, resource);

    [Fact]
    public void OnlyResourcesThatStartWithTheBundleHeaderAreListed()
    {
        string dll = Path.Combine(Data, "Mod.dll");
        TestDll.Write(dll, [("a.bundle", "UnityFSbody"u8.ToArray()), ("b.txt", "plain text"u8.ToArray())]);
        Assert.Equal(new[] { "a.bundle" }, EmbeddedBundles(dll));
    }

    [Fact]
    public void ADllWithNoResourcesHasNoBundles()
    {
        string dll = Path.Combine(Data, "Empty.dll");
        TestDll.Write(dll);
        Assert.Empty(EmbeddedBundles(dll));
    }

    [Fact]
    public void ANamedResourceIsReadBackAsItsExactBytes()
    {
        string dll = Path.Combine(Data, "Mod.dll");
        byte[] bundle = "UnityFS\u0001\u0002\u0003"u8.ToArray();
        TestDll.Write(dll, [("a.bundle", bundle), ("b.txt", "text"u8.ToArray())]);
        Assert.Equal(bundle, FromDll(dll, "a.bundle"));
        Assert.Equal("text"u8.ToArray(), FromDll(dll, "b.txt"));
    }

    [Fact]
    public void AResourceTheDllDoesNotHoldGivesNothing()
    {
        string dll = Path.Combine(Data, "Mod.dll");
        TestDll.Write(dll, [("a.bundle", "UnityFSbody"u8.ToArray())]);
        Assert.Null(FromDll(dll, "missing.bundle"));
    }
}
