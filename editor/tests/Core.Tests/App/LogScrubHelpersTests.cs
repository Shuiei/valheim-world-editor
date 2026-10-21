using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The address and path checks the log's scrubbing uses: this computer's own addresses are kept as they are, and the
/// last part of a path is the part after the last separator, on either system's separators.
/// </summary>
public sealed class LogScrubHelpersTests
{
    /// <summary>Localhost by name, in any case, is this computer.</summary>
    [Fact]
    public void LocalhostByNameIsThisComputerWhateverItsCase()
    {
        Assert.True(IsThisComputer("localhost"));
        Assert.True(IsThisComputer("LOCALHOST"));
    }

    /// <summary>Any 127.x.x.x address is this computer.</summary>
    [Fact]
    public void A127AddressIsThisComputer()
    {
        Assert.True(IsThisComputer("127.0.0.1"));
        Assert.True(IsThisComputer("127.12.0.9"));
    }

    /// <summary>The IPv6 loopback is this computer, bare or in brackets.</summary>
    [Fact]
    public void TheIPv6LoopbackIsThisComputerBareOrBracketed()
    {
        Assert.True(IsThisComputer("::1"));
        Assert.True(IsThisComputer("[::1]"));
    }

    /// <summary>Another machine's address, or a name that only starts with localhost, is not this computer.</summary>
    [Fact]
    public void OtherAddressesAndNamesAreNotThisComputer()
    {
        Assert.False(IsThisComputer("10.0.0.1"));
        Assert.False(IsThisComputer("192.168.1.127"));
        Assert.False(IsThisComputer("localhost.example.com"));
    }

    /// <summary>A path with forward slashes ends at its last slash.</summary>
    [Fact]
    public void LastPartOfAForwardSlashPathIsTheFileName()
    {
        Assert.Equal("Log.cs", LastPart("/home/user/src/Log.cs"));
    }

    /// <summary>A path with backslashes ends at its last backslash.</summary>
    [Fact]
    public void LastPartOfABackslashPathIsTheFileName()
    {
        Assert.Equal("worlds", LastPart("C:\\Users\\me\\worlds"));
    }

    /// <summary>A name with no separator is its own last part.</summary>
    [Fact]
    public void LastPartOfANameWithoutSeparatorIsTheName()
    {
        Assert.Equal("World20118", LastPart("World20118"));
    }

    /// <summary>A path ending in a separator has an empty last part.</summary>
    [Fact]
    public void LastPartOfAFolderWithATrailingSeparatorIsEmpty()
    {
        Assert.Equal("", LastPart("/home/user/worlds/"));
    }

    /// <summary>Calls the log's address check.</summary>
    private static bool IsThisComputer(string address) =>
        (bool)PrivateMember.Call(typeof(Log), "IsThisComputer", null, address)!;

    /// <summary>Calls the log's last-part helper.</summary>
    private static string LastPart(string path) =>
        (string)PrivateMember.Call(typeof(Log), "LastPart", null, path)!;
}
