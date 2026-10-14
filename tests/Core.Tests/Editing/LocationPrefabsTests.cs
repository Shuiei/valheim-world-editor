using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's location prefabs as the dungeon scan describes them (tests/fixtures/dungeon-rooms.json: the start temple
/// and a crypt's entrance), and what the game makes of one when it spawns it.
/// </summary>
public class LocationPrefabsTests
{
    [Fact]
    public void TheCatalogueDescribesTheStartTemple()
    {
        var temple = LocationPrefabs.Get("StartTemple");
        Assert.NotNull(temple);
        Assert.Equal(("StartTemple", StableHash.Of("StartTemple"), false, true), (temple.Name, temple.Hash, temple.Interior, temple.ClearArea));
        // Its boss stones are networked objects of their own, standing around it.
        Assert.Contains(temple.Contents, c => c.Prefab == "BossStone_Eikthyr");
        Assert.All(temple.Contents, c => Assert.InRange(c.Position.Length(), 0f, 40f));
        Assert.Equal(temple.Nodes.Length, temple.Nodes.Count(n => n >= -1));
        Assert.Null(LocationPrefabs.Get("NoSuchPlace"));
        Assert.Contains(LocationPrefabs.All, p => p.Name == "StartTemple");
    }

    [Fact]
    public void ADungeonsEntranceHasAnInteriorTheGameLaysOut()
    {
        var crypt = LocationPrefabs.Get("Crypt2");
        Assert.NotNull(crypt);
        Assert.True(crypt.Interior);
    }

    [Fact]
    public void TheZoneSeedIsTheGames()
    {
        Assert.Equal(unchecked(-1261093776 + 3 * 4271 + -2 * 9187), LocationPrefabs.ZoneSeed(-1261093776, 3, -2));
        Assert.Equal(7, LocationPrefabs.ZoneSeed(7, 0, 0));
    }

    [Fact]
    public void WhatIsMadeStandsWithTheLocationAndIsRolledFromItsSeed()
    {
        var temple = LocationPrefabs.Get("StartTemple")!;
        var at = new Vector3(100f, 40f, -60f);
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        var made = LocationPrefabs.Made(temple, at, turn, 1234);
        Assert.InRange(made.Count, 1, temple.Contents.Length);
        // The same seed makes the same again; every object is one of the prefab's contents, moved and turned with it.
        Assert.Equal(made, LocationPrefabs.Made(temple, at, turn, 1234));
        Assert.All(made, m => Assert.Contains(temple.Contents, c => c.Prefab == m.Prefab && Vector3.Distance(at + Vector3.Transform(c.Position, turn), m.Position) < 0.001f));
        // A part no random part decides over is always there.
        foreach (var c in temple.Contents.Where(c => c.Node < 0))
        {
            Assert.Contains(made, m => m.Prefab == c.Prefab && Vector3.Distance(at + Vector3.Transform(c.Position, turn), m.Position) < 0.001f);
        }
        // The Unity random is left as it was.
        var state = ValheimGen.UnityEngine.Random.state;
        LocationPrefabs.Made(temple, at, turn, 99);
        Assert.Equal(state, ValheimGen.UnityEngine.Random.state);
    }

    [Fact]
    public void NothingIsKnownWithoutALocationsPart()
    {
        string json = File.ReadAllText(Path.Combine(Fixtures.Root, "dungeon-rooms.json"));
        try
        {
            LocationPrefabs.Use("""{"dungeons":{},"rooms":{}}""");
            Assert.Null(LocationPrefabs.Get("StartTemple"));
            Assert.Empty(LocationPrefabs.All);
        }
        finally
        {
            LocationPrefabs.Use(json);
        }
    }
}
