using System.Text.Json.Nodes;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// An optional true or false in a manifest: absent counts as false, a boolean is read, anything else is refused.
/// </summary>
public class ModManifestsFlagTests
{
    /// <summary>Calls ModManifests' flag reader on a node, with its out value.</summary>
    private static (bool Ok, bool Flag) Flag(JsonNode? node)
    {
        object?[] args = [node, false];
        bool ok = PrivateMember.Call<bool>(typeof(TerrainEditor.App.ModManifests), "Flag", null, args);
        return (ok, (bool)args[1]!);
    }

    [Fact]
    public void AnAbsentFlagIsOkAndFalse()
    {
        Assert.Equal((true, false), Flag(null));
    }

    [Fact]
    public void ATrueBooleanIsReadAsTrue()
    {
        Assert.Equal((true, true), Flag(JsonValue.Create(true)));
    }

    [Fact]
    public void AFalseBooleanIsReadAsFalse()
    {
        Assert.Equal((true, false), Flag(JsonValue.Create(false)));
    }

    [Fact]
    public void ANonBooleanFlagIsRefused()
    {
        Assert.Equal((false, false), Flag(JsonValue.Create("yes")));
    }
}
