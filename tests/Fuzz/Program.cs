using System.IO.Compression;
using System.Text;
using SharpFuzz;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

// Fuzz targets for the readers of files from outside the editor, run by libFuzzer through
// libfuzzer-dotnet: Fuzz <target>. A reader may reject an input with one of the "invalid input"
// exceptions; anything else (an index out of range, a null, running out of memory, a hang...) is a
// finding.
Dictionary<string, Action<byte[]>> targets = new()
{
    ["blueprint"] = Blueprint,
    ["homestead"] = HomesteadFile,
    ["zdo"] = Zdo,
    ["inventory"] = Inventory,
    ["dungeon"] = d => Dungeons.Read(d),
    ["zonedb"] = d => ZoneDb.Load(new MemoryStream(d)),
    ["snapshot"] = Snapshot,
    ["chunk"] = d => WorldSave.ReadChunk(new MemoryStream(d)),
    ["png"] = d => Png.Read(d),
};
if (args.Length == 0 || !targets.TryGetValue(args[0], out Action<byte[]>? run))
{
    Console.Error.WriteLine("usage: Fuzz " + string.Join(" | ", targets.Keys));
    return 2;
}
bool ready = false;
Fuzzer.LibFuzzer.Run(span =>
{
    // The game's data as the tests have it (tests/fixtures; build.sh copies it beside the program), so
    // objects are read as in a real world: terrain modifiers, runestones, ground discs. Loaded at the
    // first input: the editor's code may only run once libFuzzer has started (its coverage map).
    if (!ready)
    {
        GameData.UseCopies(FixturesFolder());
        WorldSave.ModifierPrefabs = [.. TerrainModifiers.NetworkPrefabHashes];
        ready = true;
    }
    try
    {
        run(span.ToArray());
    }
    catch (Exception e) when (e is InvalidDataException or FormatException or EndOfStreamException or System.Text.Json.JsonException)
    {
        // Rejected: as it should.
    }
});
return 0;

static string FixturesFolder()
{
    for (string? d = AppContext.BaseDirectory; d != null; d = Path.GetDirectoryName(d))
    {
        foreach (string f in new[] { Path.Combine(d, "fixtures"), Path.Combine(d, "tests", "fixtures") })
        {
            if (File.Exists(Path.Combine(f, "prefabs.json")))
            {
                return f;
            }
        }
    }
    throw new DirectoryNotFoundException("tests/fixtures not found above " + AppContext.BaseDirectory);
}

// A blueprint file: its first byte picks the format by file name, the rest is its text.
static void Blueprint(byte[] data)
{
    if (data.Length == 0)
    {
        return;
    }
    string name = (data[0] % 3) switch { 0 => "x.blueprint", 1 => "x.vbuild", _ => "x.txt" };
    BlueprintFormats.Parse(name, Encoding.UTF8.GetString(data, 1, data.Length - 1));
}

// A Homestead blueprint file, line by line as File.ReadLines gives them.
static void HomesteadFile(byte[] data)
{
    using StringReader text = new(Encoding.UTF8.GetString(data));
    Homestead.Read("x.blueprint", Lines(text));

    static IEnumerable<string> Lines(StringReader text)
    {
        while (text.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}

// One saved object: its first byte picks the world version it was saved in (chunked saves, older
// saves with a sector, before counts took two bytes), the rest is the object. Written back after.
static void Zdo(byte[] data)
{
    if (data.Length == 0)
    {
        return;
    }
    int version = (data[0] % 3) switch { 0 => 41, 1 => 39, _ => 32 };
    ZdoData.Parse(data[1..], version).Serialize();
}

// A container's contents; an older item format is refused (NotSupportedException), as the editor
// expects. Written back after.
static void Inventory(byte[] data)
{
    try
    {
        InventoryData.Read(data).Write();
    }
    catch (NotSupportedException)
    {
    }
}

// A live snapshot: its first byte says whether the rest is the snapshot as the bridge sends it
// (gzip, odd) or its content, gzipped here (even): mutations of gzip itself rarely get past its
// checksum to the content.
static void Snapshot(byte[] data)
{
    if (data.Length == 0)
    {
        return;
    }
    byte[] rest = data[1..];
    if (data[0] % 2 == 0)
    {
        using MemoryStream packed = new();
        using (GZipStream gz = new(packed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gz.Write(rest);
        }
        rest = packed.ToArray();
    }
    WorldSave.LoadLive(rest, "fuzz");
}
