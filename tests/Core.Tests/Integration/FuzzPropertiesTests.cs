using System.Numerics;
using FsCheck;
using FsCheck.Xunit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// Properties of the readers of files from outside the editor (what tests/Fuzz fuzzes), checked on inputs FsCheck
/// makes up: whatever they are given, they read it or refuse it as damaged, and what the editor writes reads back
/// to the same bytes.
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

    /// <summary>The test world's first object, as saved (read once: the world is only read).</summary>
    private static readonly Lazy<byte[]> FirstObject = new(() => WorldSave.Load(Fixtures.World).ObjectBytes(0));

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

    [Property(MaxTest = 200)]
    public bool AnObjectsValuesReadBackAsWritten(int[] ints, long[] longs, NonNull<string>[] strings, byte[][] bytes)
    {
        var z = ZdoData.Parse(FirstObject.Value);
        for (int i = 0; i < ints.Length; i++)
        {
            z.Set("ints", 1000 + i, ints[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        for (int i = 0; i < longs.Length; i++)
        {
            z.Set("longs", 2000 + i, longs[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        for (int i = 0; i < strings.Length; i++)
        {
            z.Set("strings", 3000 + i, strings[i].Get);
        }
        for (int i = 0; i < bytes.Length; i++)
        {
            z.SetBytes(4000 + i, bytes[i] ?? []);
        }
        byte[] written = z.Serialize();
        return ZdoData.Parse(written).Serialize().AsSpan().SequenceEqual(written);
    }

    [Property(MaxTest = 300)]
    public bool ContainerContentsReadBackAsWritten(int[] prefabs, byte[] counts, bool equipped)
    {
        InventoryData inventory = new();
        for (int i = 0; i < prefabs.Length; i++)
        {
            byte n = counts.Length > 0 ? counts[i % counts.Length] : (byte)1;
            inventory.Items.Add(new InventoryData.Item { Prefab = prefabs[i], Stack = n, X = i % 8, Y = i / 8, Quality = 1 + (n % 4), Durability = n * 1.5f, Equipped = equipped && i == 0 });
        }
        byte[] written = inventory.Write();
        return InventoryData.Read(written).Write().AsSpan().SequenceEqual(written);
    }

    // The save keeps rotations as Euler angles and the editor as quaternions: a rotation comes back as the
    // same rotation (to float precision), hashes and positions exactly.
    [Property(MaxTest = 300)]
    public bool ADungeonsRoomsReadBackAsWritten(int[] hashes, short[] numbers)
    {
        float N(int i) => numbers.Length > 0 ? numbers[i % numbers.Length] / 10f : 0f;
        List<Dungeons.Placed> rooms = [.. hashes.Select((h, i) => new Dungeons.Placed(h, new Vector3(N(i), N(i + 1), N(i + 2)), BlueprintFormats.FromEuler(new Vector3(N(i + 3), N(i + 4), N(i + 5)))))];
        var back = Dungeons.Read(Dungeons.Write(rooms));
        return back.Count == rooms.Count && rooms.Zip(back).All(p =>
            p.First.Hash == p.Second.Hash && p.First.Position == p.Second.Position
            && Math.Abs(Quaternion.Dot(Quaternion.Normalize(p.First.Rotation), Quaternion.Normalize(p.Second.Rotation))) > 0.9999f);
    }

    [Property(MaxTest = 100)]
    public bool AZoneListReadsBackAsWritten(short[] zones, int[] locations)
    {
        string dir = Directory.CreateTempSubdirectory("vwe-db2-").FullName;
        try
        {
            var db = ZoneDb.Load(World("_main.2.db2"));
            foreach (short z in zones)
            {
                db.Generated.Add((z, (short)(z / 3)));
            }
            foreach (int l in locations)
            {
                db.Locations.Add((l, new Vector3(l % 1000, 30, l % 777), l % 2 == 0));
            }
            string a = Path.Combine(dir, "a.db2"), b = Path.Combine(dir, "b.db2");
            db.Save(a);
            ZoneDb.Load(a).Save(b);
            return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
