using System.Text;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A container's contents: each item's fields come back as they were written, and a format older than the editor reads
/// is refused.
/// </summary>
public class InventoryDataRoundTripTests
{
    [Fact]
    public void AnItemComesBackWithEveryFieldItWasWrittenWith()
    {
        var inv = new InventoryData();
        var item = new InventoryData.Item
        {
            Prefab = 42,
            Stack = 3,
            Quality = 2,
            Variant = 1,
            X = 2,
            Y = 3,
            WorldLevel = 1,
            Equipped = true,
            CrafterId = 7,
            CrafterName = "bob",
            Cheated = true,
        };
        item.CustomData["k"] = "v";
        inv.Items.Add(item);

        var got = Assert.Single(InventoryData.Read(inv.Write()).Items);

        Assert.Equal(42, got.Prefab);
        Assert.Equal(3, got.Stack);
        Assert.Equal(2, got.Quality);
        Assert.Equal(1, got.Variant);
        Assert.Equal(2, got.X);
        Assert.Equal(3, got.Y);
        Assert.Equal(1, got.WorldLevel);
        Assert.True(got.Equipped);
        Assert.Equal(7, got.CrafterId);
        Assert.Equal("bob", got.CrafterName);
        Assert.True(got.Cheated);
        Assert.Equal("v", got.CustomData["k"]);
    }

    [Fact]
    public void AnEmptyContainerWritesItsHeaderOnly()
    {
        var back = InventoryData.Read(new InventoryData().Write());

        Assert.Empty(back.Items);
    }

    [Fact]
    public void AFormatOlderThanTheEditorReadsIsRefused()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(107);
            w.Write((ushort)0);
        }

        Assert.Throws<NotSupportedException>(() => InventoryData.Read(ms.ToArray()));
    }
}
