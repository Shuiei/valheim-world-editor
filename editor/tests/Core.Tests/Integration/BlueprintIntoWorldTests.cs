using System.Numerics;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.AppDataPrefabs;

namespace WorldEditor.Tests;

/// <summary>
/// A building with leaning pieces, written as a blueprint, read back, saved into the test world as new player-built
/// pieces and the world read again: each piece keeps its pitch and roll, not only its turn about y.
/// </summary>
[Collection("World files")]
public class BlueprintIntoWorldTests
{
    [Fact]
    public void LeaningPiecesOfABlueprintKeepTheirPitchAndRollInTheWorld()
    {
        // A slide's floor (pitched and rolled) and wall (rolled), as the Workshop holds them.
        (string Name, Vector3 Euler)[] pieces = [("wood_floor", new Vector3(11.3f, 92.9f, 348f)), ("wood_wall_half", new Vector3(0f, 2.9f, 348.7f))];
        var clip = new JsonObject
        {
            ["objects"] = new JsonArray([.. pieces.Select((p, k) => (JsonNode)new JsonObject
            {
                ["name"] = p.Name,
                ["dx"] = 4f * k,
                ["dy"] = 0f,
                ["dz"] = 0f,
                ["rx"] = p.Euler.X,
                ["ry"] = p.Euler.Y,
                ["rz"] = p.Euler.Z,
            })]),
        };
        string text = Homestead.Write(clip, "Slide", "Tester", null, new DateTime(2026, 10, 24, 21, 48, 0, DateTimeKind.Local));
        var parsed = BlueprintFormats.Parse("Slide.blueprint", text);
        Assert.Equal(2, parsed.Pieces.Count);

        using var w = new TempWorld();
        WorldSave world = w.Load();
        Vector3 near = world.Objects.First(o => o.Prefab == Beech).Position + new Vector3(3, 0, 3);
        var added = parsed.Pieces.Select((p, k) => new NewObject(-1 - k, StableHash.Of(p.Name), near + p.Position, p.Euler, 0f)).ToList();
        long before = WorldSave.Builder;
        try
        {
            WorldSave.Builder = 777;
            var r = WorldWriter.Save(world, [], added: added);
            Assert.True(r.Saved, r.Message);
        }
        finally
        {
            WorldSave.Builder = before;
        }

        world = w.Load();
        foreach (var (name, euler) in pieces)
        {
            var (_, _, _, rotation) = Assert.Single(world.Pieces, p => p.Prefab == StableHash.Of(name) && added.Any(a => a.Prefab == p.Prefab && Vector3.Distance(a.Position, p.Position) < 0.01f));
            // The save keeps each angle to the half degree (the game's small rotation): the turn is that close.
            Assert.True(Degrees(euler, rotation) < 1f, $"{name}: saved {euler}, read {rotation}");
        }
    }

    /// <summary>The angle in degrees between two rotations given as Unity Euler angles.</summary>
    private static float Degrees(Vector3 a, Vector3 b)
    {
        float dot = MathF.Abs(Quaternion.Dot(BlueprintFormats.FromEuler(a), BlueprintFormats.FromEuler(b)));
        return 2f * MathF.Acos(MathF.Min(1f, dot)) * 180f / MathF.PI;
    }
}
