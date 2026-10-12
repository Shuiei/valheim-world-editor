using TerrainEditor.App;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Blueprint files written for tests: a small Homestead hut (one floor floating, one unknown kind) and .vbuild files
/// from another mod.
/// </summary>
internal static class BlueprintFiles
{
    /// <summary>
    /// Writes a Homestead blueprint (a floor, a wall, a floor floating 12 m up and an unknown kind) into the folder;
    /// returns its path.
    /// </summary>
    public static string Hut(string dir, string name = "Hut")
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, Homestead.FileName(name));
        File.WriteAllText(path, "#Name:" + name + "\n#Creator:test\n#HomesteadVersion:1\n#Pieces\n"
            + "wood_floor;Building;0;0;0;0;0;0;1;\"\";1;1;1\n"
            + "woodwall;Building;1;1;0;0;0.707;0;0.707;\"\";1;1;1\n"
            + "wood_floor;Building;0;12;0;0;0;0;1;\"\";1;1;1\n"
            + "no_such_piece_xyz;Building;0;0;0;0;0;0;1;\"\";1;1;1\n");
        return path;
    }

    /// <summary>Writes a .vbuild file named <c>name</c> holding one object of each kind given; returns its path.</summary>
    public static string Vbuild(string dir, string name, params string[] kinds)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name + ".vbuild");
        var clip = System.Text.Json.Nodes.JsonNode.Parse(CopyFormat.ToJson(BlueprintsPanelRun.Clip(name, kinds)).ToJsonString())!.AsObject();
        File.WriteAllText(path, BlueprintFormats.Write(clip, "vbuild", name, _ => 0));
        return path;
    }
}
