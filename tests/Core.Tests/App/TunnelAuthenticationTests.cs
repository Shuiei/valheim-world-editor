using Renci.SshNet;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The SSH methods a tunnel tries: a key file that cannot be read is an error that stops the connection with the reason,
/// and a password is always offered when one is given. A read error the key cannot cause (a bug) is not taken as an
/// unreadable key.
/// </summary>
[Collection("DataDir")]
public sealed class TunnelAuthenticationTests
{
    /// <summary>A request to a local host as root, with the key path and password given.</summary>
    private static Tunnel.Request Entry(string? keyPath, string? password) =>
        new("127.0.0.1", 22, "root", password, keyPath, null, null, null, false, null);

    /// <summary>A file or a key format that cannot be read is a key that cannot be used.</summary>
    [Fact]
    public void AFormatErrorMeansTheKeyCannotBeRead() =>
        Assert.True(TunnelAuthentication.KeyUnreadable(new FormatException("bad key")));

    /// <summary>A missing file is an I/O error and means the key cannot be read.</summary>
    [Fact]
    public void AnIOErrorMeansTheKeyCannotBeRead() =>
        Assert.True(TunnelAuthentication.KeyUnreadable(new IOException("gone")));

    /// <summary>A timeout is not a key error, so it is not taken as an unreadable key.</summary>
    [Fact]
    public void AnErrorThatIsNoKeyErrorIsNotTakenAsAnUnreadableKey() =>
        Assert.False(TunnelAuthentication.KeyUnreadable(new TimeoutException()));

    /// <summary>A chosen key file that does not exist gives no methods, and the reason the key could not be read.</summary>
    [Fact]
    public void AMissingChosenKeyStopsWithTheReason()
    {
        string missing = Path.Combine(Path.GetTempPath(), "vwe-no-key-" + Guid.NewGuid().ToString("N")[..8]);

        var (methods, error) = TunnelAuthentication.Methods(Entry(missing, null), "root", []);

        Assert.Empty(methods);
        Assert.NotNull(error);
        Assert.StartsWith("The key file could not be read:", error);
    }

    /// <summary>A given password is one of the methods, with no error.</summary>
    [Fact]
    public void AGivenPasswordIsOfferedAsAMethod()
    {
        var (methods, error) = TunnelAuthentication.Methods(Entry(null, "secret"), "root", []);

        Assert.Null(error);
        Assert.Contains(methods, m => m is PasswordAuthenticationMethod);
    }
}
