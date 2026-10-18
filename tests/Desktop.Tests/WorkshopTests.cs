using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Workshop's blueprints holding objects: a chest's contents, a creature's stars and a ruin's lack of builder go through
/// a blueprint, the Workshop and the blueprint saved from it; and an object field reads back as it was written.
/// </summary>
public class WorkshopTests
{
    // What objects hold (a chest's contents, a creature's stars) and a ruin's lack of builder go through a
    // blueprint, the Workshop, and the blueprint saved from it.
    [Fact]
    public void WhatObjectsHoldGoesThroughBlueprintsAndTheWorkshop()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-ws-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(dir);
            string items = Convert.ToBase64String(ObjectData.BuildInventory([new ItemUpload("CryptKey", null), new ItemUpload("Coins", null, 50, X: 1)]).Write());
            System.Text.Json.Nodes.JsonObject Obj(string name, float dx, params ObjectField[] data) => new()
            {
                ["name"] = name,
                ["dx"] = dx,
                ["dy"] = 0,
                ["dz"] = 0,
                ["rx"] = 0,
                ["ry"] = 0,
                ["rz"] = 0,
                ["scale"] = 0,
                ["data"] = BlueprintFormats.DataJson(data.Append(ObjectField.NoBuilder)),
            };
            var clip = new System.Text.Json.Nodes.JsonObject
            {
                ["objects"] = new System.Text.Json.Nodes.JsonArray(
                    Obj("TreasureChest_sunkencrypt", 0, new ObjectField("bytes", ObjectData.ItemsKey, items), new ObjectField("ints", ObjectData.AddedDefaultItemsKey, "1")),
                    Obj("Draugr_Elite", 4, new ObjectField("ints", StableHash.Of("level"), "3")),
                    Obj("stone_wall_4x2", -3)),
            };
            string text = Homestead.Write(clip, "Vault", "test", null, DateTime.Now);
            Assert.Contains(BlueprintFormats.RuinHeader, text);
            string path = Path.Combine(dir, "vault.blueprint");
            File.WriteAllText(path, text);
            var parsed = BlueprintFormats.Parse("vault.blueprint", text);
            Assert.True(parsed.Ruin);
            Assert.Contains(parsed.Pieces, p => p.Name == "Draugr_Elite" && p.Data!.Contains(new ObjectField("ints", StableHash.Of("level"), "3")));

            var scene = Workshop.Create("Workshop");
            var (placed, unknown, lift, _) = Workshop.Open(scene.Session!, path);
            Assert.Equal(3, placed);
            Assert.Empty(unknown);
            ZdoData Held(string name)
            {
                var t = scene.Things.Single(t => !t.Gone && t.Prefab == StableHash.Of(name));
                return ZdoData.Parse(ObjectData.Bytes(scene.World, scene.Session!.Edits, t.Id)!);
            }
            var chest = Held("TreasureChest_sunkencrypt");
            Assert.Contains(InventoryData.Read(chest.GetBytes(ObjectData.ItemsKey)!).Items, i => i.Prefab == StableHash.Of("CryptKey"));
            Assert.Contains(chest.IntList, i => i.Key == ObjectData.AddedDefaultItemsKey && i.Value == 1);
            Assert.Contains(Held("Draugr_Elite").IntList, i => i.Key == StableHash.Of("level") && i.Value == 3);
            Assert.DoesNotContain(Held("stone_wall_4x2").LongList, l => l.Key == ObjectField.CreatorKey);

            // Saved from the Workshop: all three, holding the same, still a ruin.
            var again = BlueprintFormats.Parse("vault.blueprint", Homestead.Write(Workshop.Building(scene, "Vault", lift), "Vault", "test", null, DateTime.Now));
            Assert.True(again.Ruin);
            Assert.Equal(3, again.Pieces.Count);
            var data = again.Pieces.Single(p => p.Name == "TreasureChest_sunkencrypt").Data!;
            Assert.Contains(data, f => f.Section == "bytes" && f.Key == ObjectData.ItemsKey
                && InventoryData.Read(Convert.FromBase64String(f.Value!)).Items.Any(i => i.Prefab == StableHash.Of("CryptKey")));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void AnObjectFieldReadsBackAsItWasWritten()
    {
        foreach (var f in new[] { new ObjectField("strings", 12, "a;b\nc"), new ObjectField("longs", -5, null), new ObjectField("vec3", 1, "1 2 3") })
        {
            Assert.Equal(f, ObjectField.Parse(f.ToString()));
        }
        Assert.Null(ObjectField.Parse("nothing;1;x"));
        // Base64 the save cannot take as that section (a damaged line): left out, not kept for the save to fail on.
        static string B(string v) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(v));
        Assert.Null(ObjectField.Parse("floats;1;" + B("abc")));
        Assert.Null(ObjectField.Parse("vec3;1;" + B("1 2")));
        Assert.Null(ObjectField.Parse("bytes;1;" + B("not base64!")));
    }
}
