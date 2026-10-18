using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The mods' extensions drawn by the real editor (OpenGL): the SDK's sample extension, run from a plugins folder the
/// settings name (ExtensionEditorProcess), draws its sail and its cable as shapes over the area and cuts the ground round
/// its pit, without an OpenGL error; the shapes are made again as the area's things change.
/// </summary>
[Collection("Visual extensions")]
[Trait("Category", "Visual")]
public sealed class ExtensionVisualTests(ExtensionEditorProcess editor) : VisualPicturesTestBase("extension-pics")
{
    [Fact]
    public void TheSampleExtensionsShapesAndCutsAreDrawn()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        Directory.CreateDirectory(Dir);
        editor.Send($"world {ExtensionEditorProcess.World}");
        Assert.Equal("editor", editor.Send("area 0 0 1").GetProperty("page").GetString());
        Assert.Contains("extension ExampleVehicles (ExampleVehicles.WorldEditor.dll): running.", Log());
        editor.Send("camera 0 0 0 60 120");
        var look = Picture("shapes");
        Assert.True(look.Spread > 4, $"the picture is flat (brightness spread {look.Spread:0.0})");
        Assert.True(look.Colours > 20, $"the picture has few colours ({look.Colours})");
        // The ground raised under them: the area's things are sent again, and the shapes with them.
        Assert.True(editor.Send("stroke raise 32 32 5").GetProperty("pending").GetInt32() >= 1);
        Picture("shapes-again");
        Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
        editor.Send("map");
    }

    /// <summary>What the editor said so far: its log in its data folder (its console goes there too).</summary>
    private string Log() => File.ReadAllText(Path.Combine(editor.Data, "ValheimWorldEditor.log"));

    /// <summary>A picture of the 3D view, measured.</summary>
    private PictureLook Picture(string name)
    {
        string path = Path.Combine(Dir, name + ".png");
        editor.Send($"picture {path}");
        return PictureLook.Of(path);
    }
}
