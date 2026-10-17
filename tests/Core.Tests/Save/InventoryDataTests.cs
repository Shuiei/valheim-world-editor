using System.Text;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Items in the save format (InventoryData): an item's durability comes back as it was after a write and a read.
/// </summary>
public class InventoryDataTests
{
    // Kept in hundredths: every hundredth from 0 to 2000 comes back the same (a write cut about one in five, 0.29 among
    // them, a hundredth lower).
    [Fact]
    public void DurabilityIsKeptThroughAWriteAndARead()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            for (int h = 0; h <= 200_000; h++)
            {
                InventoryData.WriteItem(w, new InventoryData.Item { Prefab = 1, Durability = h * 0.01f });
            }
        }
        ms.Position = 0;
        using var r = new BinaryReader(ms, Encoding.UTF8);

        for (int h = 0; h <= 200_000; h++)
        {
            Assert.Equal(h * 0.01f, InventoryData.ReadItem(r, InventoryData.Version).Durability);
        }
    }
}
