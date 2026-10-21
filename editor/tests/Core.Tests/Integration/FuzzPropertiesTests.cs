using FsCheck;
using FsCheck.Xunit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// Properties of the readers of files from outside the editor (what editor/tests/Fuzz fuzzes), checked on inputs FsCheck
/// makes up: whatever they are given, they read it or refuse it as damaged. The files the editor writes read back to the
/// same bytes in FuzzRoundTripPropertiesTests.
/// </summary>
public class FuzzPropertiesTests
{
    /// <summary>Whether reading succeeded or was refused with one of the "damaged input" exceptions.</summary>
    private static bool ReadsOrRefuses(Action read)
    {
        try
        {
            read();
            return true;
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or FormatException or System.Text.Json.JsonException)
        {
            return true;
        }
    }

    /// <summary>A real file of the test world, cut short at a point taken from <paramref name="cut"/>.</summary>
    private static byte[] Cut(string file, NonNegativeInt cut)
    {
        byte[] all = File.ReadAllBytes(file);
        return all[..(cut.Get % (all.Length + 1))];
    }

    private static string World(string name) => Path.Combine(Fixtures.World, name);

    [Property(MaxTest = 300)]
    public bool AnyBytesAreReadOrRefusedAsAnObject(byte[] data) => ReadsOrRefuses(() => ZdoData.Parse(data));

    [Property(MaxTest = 300)]
    public bool AnyBytesAreReadOrRefusedAsContainerContents(byte[] data) => ReadsOrRefuses(() =>
    {
        try
        {
            InventoryData.Read(data);
        }
        catch (NotSupportedException)
        {
            // An item format older than the editor reads: refused as such, by design.
        }
    });

    [Property(MaxTest = 300)]
    public bool AnyBytesAreReadOrRefusedAsADungeon(byte[] data) => ReadsOrRefuses(() => Dungeons.Read(data));

    [Property(MaxTest = 300)]
    public bool AnyBytesAreReadOrRefusedAsAZoneList(byte[] data) => ReadsOrRefuses(() => ZoneDb.Load(new MemoryStream(data)));

    [Property(MaxTest = 300)]
    public bool AnyBytesAreReadOrRefusedAsAChunk(byte[] data) => ReadsOrRefuses(() => WorldSave.ReadChunk(new MemoryStream(data)));

    [Property(MaxTest = 300)]
    public bool AnyBytesAreReadOrRefusedAsALiveSnapshot(byte[] data) => ReadsOrRefuses(() => WorldSave.LoadLive(data, "test"));

    [Property(MaxTest = 300)]
    public bool AnyBytesAreReadOrRefusedAsAPicture(byte[] data) => ReadsOrRefuses(() => Png.Read(data));

    [Property(MaxTest = 300)]
    public bool AnyTextIsReadOrRefusedAsABlueprint(NonNull<string> text, bool vbuild) =>
        ReadsOrRefuses(() => BlueprintFormats.Parse(vbuild ? "x.vbuild" : "x.blueprint", text.Get));

    [Property(MaxTest = 300)]
    public bool AnyTextIsReadOrRefusedAsAHomesteadBlueprint(NonNull<string> text) =>
        ReadsOrRefuses(() => Homestead.Read("x.blueprint", text.Get.Split('\n')));

    [Property(MaxTest = 100)]
    public bool ACutChunkFileIsReadOrRefused(NonNegativeInt cut) =>
        ReadsOrRefuses(() => WorldSave.ReadChunk(new MemoryStream(Cut(World("00_00__0_2.chunk"), cut))));

    [Property(MaxTest = 100)]
    public bool ACutZoneListIsReadOrRefused(NonNegativeInt cut) =>
        ReadsOrRefuses(() => ZoneDb.Load(new MemoryStream(Cut(World("_main.2.db2"), cut))));

    [Property(MaxTest = 100)]
    public bool ACutLiveSnapshotIsReadOrRefused(NonNegativeInt cut) =>
        ReadsOrRefuses(() => WorldSave.LoadLive(Cut(Fixtures.Snapshot, cut), "test"));
}
