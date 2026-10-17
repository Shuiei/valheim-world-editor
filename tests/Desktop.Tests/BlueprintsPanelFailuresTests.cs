using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Blueprints panel's failures: a blueprint that cannot be read, exported or imported is said so, and a folder that
/// cannot be written is said so and nothing is lost.
/// </summary>
public class BlueprintsPanelFailuresTests
{
    [AvaloniaFact]
    public void BlueprintsThatCannotBeReadAreSaidSo()
    {
        using var r = new BlueprintsPanelRun();
        r.B.Paste("no-such-blueprint");
        Assert.Equal("That blueprint could not be read.", r.Said);
        r.B.Paste("hs:../outside.blueprint");
        Assert.Equal("That blueprint could not be read.", r.Said);
        Assert.Null(r.B.Export("no-such-blueprint", "vbuild"));
        Assert.Equal("Could not export that blueprint.", r.Said);
        Assert.Null(r.B.Import(Path.Combine(r.Dir, "missing.vbuild")));
        Assert.StartsWith("Could not import that file: ", r.Said);
        Directory.CreateDirectory(r.Dir);
        string empty = Path.Combine(r.Dir, "empty.vbuild");
        File.WriteAllText(empty, "nothing useful here");
        Assert.Null(r.B.Import(empty));
        Assert.Contains("no pieces found in it", r.Said);
        // Terrain marks too far apart for a ground grid: said, on an import and on a paste, export or copy to Homestead
        // of a Homestead file.
        string wide = string.Join("\n", "#Terrain", "circle;0;0;0;1;0;0;dirt", "circle;2000;0;0;1;0;0;dirt");
        string wideFile = Path.Combine(r.Dir, "wide.blueprint");
        File.WriteAllText(wideFile, wide);
        Assert.Null(r.B.Import(wideFile));
        Assert.StartsWith("Could not import that file: its ground marks span ", r.Said);
        Directory.CreateDirectory(r.Homestead);
        File.WriteAllText(Path.Combine(r.Homestead, "wide.blueprint"), wide);
        r.B.Paste("hs:wide.blueprint");
        Assert.StartsWith("That blueprint could not be read: its ground marks span ", r.Said);
        r.Said = null;
        Assert.Null(r.B.Export("hs:wide.blueprint", "vbuild"));
        Assert.StartsWith("That blueprint could not be read: its ground marks span ", r.Said);
        r.Said = null;
        Assert.Null(r.B.ToHomestead("hs:wide.blueprint"));
        Assert.StartsWith("That blueprint could not be read: its ground marks span ", r.Said);
        // An older editor blueprint whose ground arrays are shorter than its size says: said, nothing pasted.
        var shortClip = CopyFormat.ToJson(BlueprintsPanelRun.Clip("Short", "woodwall"));
        shortClip["rel"] = new System.Text.Json.Nodes.JsonArray(0);
        r.B.Paste(r.B.Store.Save("Short", null, null, shortClip));
        Assert.StartsWith("That blueprint could not be read: its \"rel\" list holds 1 values", r.Said);
        Assert.Null(r.W.View.Paste.Clip);
    }

    // A blueprints folder that cannot be written (here a file where the folder should be) is said so;
    // it closed the editor, losing what was not saved (an import is also a drop on the 3D view).
    [AvaloniaFact]
    public void AFolderThatCannotBeWrittenIsSaidSo()
    {
        using var r = new BlueprintsPanelRun();
        r.Keep("Gate", "woodwall");
        string file = r.B.Export("hs:Gate.blueprint", "vbuild")!;
        Directory.Delete(r.Homestead, recursive: true);
        File.WriteAllText(r.Homestead, "not a folder");
        Assert.Null(r.B.Import(file));
        Assert.StartsWith("Could not write in the blueprints folder: ", r.Said);
        string export = Path.GetDirectoryName(file)!;
        Directory.Delete(export, recursive: true);
        File.WriteAllText(export, "not a folder");
        File.Delete(r.Homestead);
        r.Keep("Gate", "woodwall");
        Assert.Null(r.B.Export("hs:Gate.blueprint", "vbuild"));
        Assert.StartsWith("Could not write ", r.Said);
    }
}
