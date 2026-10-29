using System.Reflection;
using System.Text.Json.Nodes;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The add-on's worldeditor.json and its code tell the same mod: it serves the mod's plugin and names the add-on's DLL;
/// every data key the code knows is labelled there, and only those; every piece is listed in the Build panel under its
/// group, a retired one hidden; the register the mod numbers trains on is data, never drawn or offered.
/// </summary>
public class ManifestTests
{
    private static readonly JsonNode Manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "worldeditor.json")))!;

    /// <summary>Every data key the code names (RailroadsMod.Keys).</summary>
    private static IEnumerable<string> CodeKeys() =>
        typeof(RailroadsMod.Keys).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void ItServesTheModsPluginWithTheAddOnsDll()
    {
        Assert.Equal(RailroadsMod.DllName, (string?)Manifest["serves"]);
        Assert.Equal(typeof(Extension).Assembly.GetName().Name + ".dll", (string?)Manifest["extension"]);
        Assert.Equal(1, (int?)Manifest["version"]);
    }

    [Fact]
    public void EveryKeyTheCodeKnowsIsLabelled()
    {
        var data = Manifest["data"]!.AsObject();

        Assert.Equal(CodeKeys().Order(StringComparer.Ordinal), data.Select(d => d.Key).Order(StringComparer.Ordinal));
        Assert.All(data, d => Assert.False(string.IsNullOrWhiteSpace((string?)d.Value!["label"])));
        // What the copies rewrite or take away is the mod's own doing: never edited in the inspector.
        Assert.All(PastedCopies.Keys, k => Assert.True((bool?)data[k]!["readOnly"] == true, k));
    }

    [Fact]
    public void ATrainAndAFlatbedAreTiedToWhatTheyName()
    {
        var ties = Manifest["ties"]!.AsArray().Select(t => ((string?)t!["root"], (string?)t["member"], (int?)t["idBytes"])).ToList();

        Assert.Equal([(RailroadsMod.Keys.Wagons, RailroadsMod.Keys.Self, ObjectIds.IdBytes), (RailroadsMod.Keys.DeckKey, RailroadsMod.Keys.Deck, null)], ties);
    }

    [Fact]
    public void EveryPieceIsListedUnderItsGroupOrHiddenWhenRetired()
    {
        var prefabs = Manifest["prefabs"]!.AsObject();

        Assert.Equal(RailroadsMod.Pieces.Select(p => p.Prefab).Append(RailroadsMod.Register).Order(StringComparer.Ordinal),
            prefabs.Select(p => p.Key).Order(StringComparer.Ordinal));
        foreach (var piece in RailroadsMod.Pieces)
        {
            var entry = prefabs[piece.Prefab]!;
            string? group = piece.Retired ? null : piece.Kind switch
            {
                RailKind.Track => "Track",
                RailKind.Station => "Stations",
                RailKind.Vehicle => "Trains",
                _ => "Other",
            };
            Assert.Equal((group, piece.Retired, false), ((string?)entry["group"], (bool?)entry["hidden"] == true, (bool?)entry["data"] == true));
        }
    }

    [Fact]
    public void TheRegisterOfTrainNumbersIsDataNeverOffered()
    {
        var register = Manifest["prefabs"]![RailroadsMod.Register]!;

        Assert.Null(register["group"]);
        Assert.Equal((true, true), ((bool?)register["hidden"] == true, (bool?)register["data"] == true));
    }
}
