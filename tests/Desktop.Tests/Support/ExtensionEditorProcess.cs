using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The editor with the SDK's sample extension (samples/ExampleVehicles.WorldEditor) running: its DLL and its
/// worldeditor.json in a mod folder under a BepInEx plugins folder the settings name, and the DLL allowed in the
/// settings by its hash, as the Settings dialog allows one. A copy of the test world is ready to open (World), the
/// sample mod's objects put in its first zone: a red four-cornered sail, two posts with a cable from one to the other,
/// a pit dug in the ground, and a canal. The editor's settings and data go to a folder of its own (Data), made before it starts.
/// </summary>
public sealed class ExtensionEditorProcess() : EditorProcess([], null, Prepared())
{
    /// <summary>The test folder: the world's copy, the mod folder and the editor's data folder.</summary>
    private static string? _dir;

    /// <summary>The id of the post the cable runs to (the mod's own id, kept in the posts' data).</summary>
    private const int FarPost = 14;

    /// <summary>The copy of the test world, with the sample mod's objects in its first zone.</summary>
    public static string World => Path.Combine(_dir!, "CITest");

    /// <summary>
    /// Makes the test folder: the world's copy with the mod's objects, the mod folder with the sample's DLL and manifest,
    /// and the editor's data folder with the settings that name the plugins folder and allow the DLL.
    /// </summary>
    /// <returns>The editor's data folder.</returns>
    private static string Prepared()
    {
        _dir = TestWorldCopy.Make("vwe-visual-ext-");
        string data = Path.Combine(_dir, "data");
        string bepInEx = Path.Combine(_dir, "mods", "BepInEx");
        string mod = Path.Combine(bepInEx, "plugins", "ExampleVehicles");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(mod);
        string dll = Path.Combine(mod, "ExampleVehicles.WorldEditor.dll");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "ExampleVehicles.WorldEditor.dll"), dll);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Samples", "worldeditor.json"), Path.Combine(mod, "worldeditor.json"));
        var settings = new AppSettings { BepInExFolders = { bepInEx } };
        settings.Extensions[dll] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(dll)));
        File.WriteAllText(Path.Combine(data, "settings.json"), JsonSerializer.Serialize(settings));
        AddObjects(World);
        return data;
    }

    /// <summary>
    /// Puts the sample mod's objects into the world's first zone, written with their data as the game saves them (the
    /// editor knows nothing of their kinds): a red sail 3 m wide and 4 m high standing on the ground, a post with a
    /// cable to the post the mod's id names, and a pit 3 m across.
    /// </summary>
    /// <param name="world">The world's folder.</param>
    private static void AddObjects(string world)
    {
        static NewObject Thing(string prefab, Vector3 at, Action<ZdoData> fill)
        {
            var z = new ZdoData { Prefab = StableHash.Of(prefab) };
            fill(z);
            return new NewObject(0, z.Prefab, at, Vector3.Zero, 1, Fresh: false, Raw: z.Serialize());
        }
        static void Int(ZdoData z, string key, int value) => z.IntList.Add((StableHash.Of(key), value));
        NewObject[] added =
        [
            // At about the ground's height there (the test world's first zone lies around 45 m).
            Thing("example_sail", new Vector3(4, 46, 4), z =>
            {
                Int(z, "ExampleVehicles_Corners", 4);
                Vector3[] corners = [Vector3.Zero, new(0, 4, 0), new(3, 4, 0), new(3, 0, 0)];
                for (int i = 0; i < corners.Length; i++)
                {
                    z.Vec3List.Add((StableHash.Of("ExampleVehicles_Corner" + (i + 1).ToString(CultureInfo.InvariantCulture)), corners[i]));
                }
                z.ByteList.Add((StableHash.Of("ExampleVehicles_Colour"), [255, 0, 0, 255]));
            }),
            Thing("example_post", new Vector3(-6, 46, 2), z => Int(z, "ExampleVehicles_Target", FarPost)),
            Thing("example_post", new Vector3(-6, 46, 10), z => Int(z, "ExampleVehicles_Id", FarPost)),
            Thing("example_pit", new Vector3(8, 46, -6), z => z.FloatList.Add((StableHash.Of("ExampleVehicles_Radius"), 1.5f))),
            // A canal along three points, 3 m wide and 1.5 m deep (the sample's defaults), kept in an object of its own.
            Thing("example_canal", new Vector3(-12, 46, -14), z => z.ByteList.Add((StableHash.Of("ExampleVehicles_Path"),
                ExampleVehicles.WorldEditor.Canals.WritePath([new(-12, -14), new(10, -14), new(14, 6)])))),
        ];
        var result = WorldWriter.Save(WorldSave.Load(world), [], added: added);
        if (!result.Saved)
        {
            throw new InvalidOperationException("the sample mod's objects could not be written into the test world: " + result.Message);
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        base.Dispose();
        TestWorldCopy.Remove(_dir);
    }
}
