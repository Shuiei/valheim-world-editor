using System.Numerics;
using TerrainEditor.Desktop.Tests;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// The save code's small parts at their edges: sectors and chunks at the world's border, item counts
// written in one or two bytes, heightmap pictures that cannot be read (and why), item formats older
// than the editor reads, the plugin's error answers, and a new object of a kind the game cannot make.
public class CoreSaveSmallTests
{
	[Theory]
	[InlineData(0f, 0f, 0, 0)]
	[InlineData(31.9f, -31.9f, 0, 0)]
	[InlineData(32f, -32.1f, 1, -1)]
	[InlineData(-10000f, 10000f, -156, 156)]
	public void PositionsFallInTheirSector(float x, float z, int sx, int sz) => Assert.Equal((sx, sz), ChunkMath.SectorOf(x, z));

	[Fact]
	public void TheWorldsBorderIsChecked()
	{
		Assert.Equal(0, ChunkMath.BaseChunk(-256, -256));
		Assert.Equal((ushort)(63 + (63 << 8)), ChunkMath.BaseChunk(255, 255));
		Assert.Throws<ArgumentOutOfRangeException>(() => ChunkMath.BaseChunk(256, 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => ChunkMath.BaseChunk(0, -257));
	}

	[Fact]
	public void ItemCountsAreOneOrTwoBytes()
	{
		using (var r = new ValheimReader(new MemoryStream(new byte[] { 0x7F })))
		{
			Assert.Equal(127, r.ReadNumItems(WorldVersion.NumItems));
		}
		// High bit set: two bytes, big end first.
		using (var r = new ValheimReader(new MemoryStream(new byte[] { 0x81, 0x02 })))
		{
			Assert.Equal(258, r.ReadNumItems(WorldVersion.NumItems));
		}
		// Older worlds: always one byte.
		using (var r = new ValheimReader(new MemoryStream(new byte[] { 0x81, 0x02 })))
		{
			Assert.Equal(0x81, r.ReadNumItems(WorldVersion.NumItems - 1));
		}
	}

	// A 2 × 2 heightmap picture, then changed in its header (the reader does not check checksums).
	private static byte[] Picture() => Png.WriteGray16(2, 2, new ushort[] { 0, 1000, 2000, 65535 });

	private static string Refused(byte[] bytes) => Assert.Throws<InvalidDataException>(() => Png.Read(bytes)).Message;

	[Fact]
	public void PicturesThatCannotBeReadSayWhy()
	{
		Assert.Equal(4, Png.Read(Picture()).Values.Length);
		Assert.Equal("not a PNG picture", Refused(new byte[] { 1, 2, 3 }));
		Assert.Equal("the PNG picture is cut short", Refused(Picture()[..30]));
		var interlaced = Picture();
		interlaced[28] = 1;
		Assert.StartsWith("interlaced PNG pictures are not supported", Refused(interlaced));
		var fourBit = Picture();
		fourBit[24] = 4;
		Assert.Equal("4-bit PNG pictures are not supported (8 or 16 bits)", Refused(fourBit));
		var palette = Picture();
		palette[25] = 3;
		Assert.StartsWith("palette PNG pictures are not supported", Refused(palette));
		var empty = Picture();
		empty[16] = empty[17] = empty[18] = empty[19] = 0;
		Assert.Equal("the picture is empty or too large", Refused(empty));
		// Taller than its data: the rows run out.
		var tall = Picture();
		tall[23] = 50;
		Assert.Equal("the PNG picture data is cut short", Refused(tall));
	}

	[Fact]
	public void AnItemFormatOlderThanTheEditorReadsIsRefused()
	{
		var ex = Assert.Throws<NotSupportedException>(() => InventoryData.Read(BitConverter.GetBytes(107)));
		Assert.Contains("older than the editor reads (108)", ex.Message);
	}

	[Fact]
	public async Task ThePluginsErrorAnswersAreReported()
	{
		using var game = new FakeGame();
		var live = new LiveBridge(game.Url, game.Token);
		Assert.Contains("\"world\":\"CITest\"", await live.Status());
		game.Fail["/players"] = 500;
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => live.Players());
		Assert.Equal("bridge players: 500 broken", ex.Message);
		game.Fail["/status"] = 500;
		Assert.Contains("answered with an error (500)", await LiveBridge.Check(game.Url, game.Token, TimeSpan.FromSeconds(5)));
		game.Fail["/status"] = 409;
		Assert.Contains("does not host the world", await LiveBridge.Check(game.Url, game.Token, TimeSpan.FromSeconds(5)));
		game.Fail["/status"] = 403;
		Assert.Contains("refused the token", await LiveBridge.Check(game.Url, game.Token, TimeSpan.FromSeconds(5)));
	}

	[Fact]
	public async Task ANewObjectOfAnUnknownKindIsSkippedLive()
	{
		using var game = new FakeGame();
		var live = new LiveBridge(game.Url, game.Token);
		var world = await live.LoadWorld();
		var edits = new EditStore(world);
		var tree = world.Objects.First(o => o.Prefab == StableHash.Of("Beech1"));
		edits.AddObjects(new[]
		{
			new NewObject(-1, StableHash.Of("Beech1"), tree.Position + new Vector3(3, 0, 3), Vector3.Zero, 1f),
			new NewObject(-2, StableHash.Of("NoSuchThing_xyz"), new Vector3(7, 30, 9), Vector3.Zero, 1f),
		});
		var sync = new LiveSync();
		string msg = await sync.Apply(world, edits, live);
		Assert.Equal((0, 1), Assert.Single(game.ObjectCalls));
		Assert.Contains("1 skipped (unknown kind of object at 7, 9)", msg);
	}
}
