using System.Numerics;
using FsCheck;
using FsCheck.Xunit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// Properties of what the editor writes: each file or value it writes reads back to the same bytes or the same values,
/// checked on inputs FsCheck makes up.
/// </summary>
public class FuzzRoundTripPropertiesTests
{
    /// <summary>The test world's first object, as saved (read once: the world is only read).</summary>
    private static readonly Lazy<byte[]> FirstObject = new(() => WorldSave.Load(Fixtures.World).ObjectBytes(0));

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
            var db = ZoneDb.Load(Path.Combine(Fixtures.World, "_main.2.db2"));
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
