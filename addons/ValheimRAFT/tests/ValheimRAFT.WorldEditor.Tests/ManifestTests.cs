using System.Text.Json.Nodes;
using Xunit;

namespace ValheimRAFT.WorldEditor.Tests;

/// <summary>The add-on's worldeditor.json against the keys and prefabs its extension reads.</summary>
public sealed class ManifestTests
{
    private readonly JsonNode _manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "worldeditor.json")))!;

    [Fact]
    public void ItServesTheModAndNamesTheExtension()
    {
        Assert.Equal("ValheimRAFT.dll", (string?)_manifest["serves"]);
        Assert.Equal(typeof(Extension).Assembly.GetName().Name + ".dll", (string?)_manifest["extension"]);
    }

    [Fact]
    public void TheVehiclesLinkByTheIdsTheRopesName()
    {
        var link = Assert.Single(_manifest["links"]!.AsArray())!;
        Assert.Equal(Keys.Id, (string?)link["id"]);
        // A sail names its mast first, a piece on a swivel the swivel, then the vehicle; a cart is carried by it.
        Assert.Equal(["SailParentId", "SwivelParentId", "MBParentId", "VehicleTempPieceParentId"], link["parents"]!.AsArray().Select(p => (string?)p));
        Assert.Equal("SailParentPosition", (string?)link["placements"]!["SailParentId"]!["offset"]);
        Assert.True((bool)link["placements"]!["VehicleTempPieceParentId"]!["carried"]!);
        // The ropes are rewritten by the extension; without it a copy leaves them out.
        Assert.Contains(Keys.Ropes, link["dropOnCopy"]!.AsArray().Select(p => (string?)p));
    }

    [Fact]
    public void ACopyIsNotDockedAndItsHullShapeIsWorkedOutAgain()
    {
        // ValheimRAFT 5.1: a docked vehicle names its dock clamp by the game's object id (two longs), and keeps the hull
        // shape it worked out with its revision; a copy keeps neither (a mirrored copy's hull would be the original's).
        var drops = Assert.Single(_manifest["links"]!.AsArray())!["dropOnCopy"]!.AsArray().Select(p => (string?)p).ToList();
        Assert.All(["vehicle_dockZdoId_u", "vehicle_dockZdoId_i", "VehicleConvexHullData", "VehicleConvexHullDataVersionHash"], k => Assert.Contains(k, drops));
    }

    [Fact]
    public void TheKeysTheExtensionReadsAreLabelled()
    {
        var data = _manifest["data"]!.AsObject();
        Assert.All(new[] { Keys.Id, Keys.Ropes, Keys.SailCorners, Keys.SailColour, Keys.PatternColour, Keys.LogoColour }.Concat(Enumerable.Range(1, 4).Select(Keys.SailCornerKey)),
            k => Assert.True(data.ContainsKey(k), k));
    }

    [Fact]
    public void ThePrefabsTheExtensionDrawsAreTheModsPieces()
    {
        var prefabs = _manifest["prefabs"]!.AsObject();
        Assert.All([Keys.Sail, Keys.RopeAnchor, Keys.DockAnchor, Keys.Swivel, Keys.LandVehicle, Keys.RopeLadder], p => Assert.NotNull(prefabs[p]?["group"]));
    }

    [Fact]
    public void TheBuildModeToggleIsNotOfferedAndTheIronBowIsAHullPiece()
    {
        // ValheimRAFT 5.1's new pieces: the build mode toggle deletes itself once placed, as the boundary tools do.
        var prefabs = _manifest["prefabs"]!.AsObject();
        Assert.True((bool)prefabs["ValheimVehicles_BuildModeToggle"]!["hidden"]!);
        Assert.All(["ValheimVehicles_hull_bow_center_iron", "ValheimVehicles_hull_bow_center_iron_inverse"], p => Assert.Equal("Hull", (string?)prefabs[p]?["group"]));
    }
}
