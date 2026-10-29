using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The library file of a blueprint named as list_blueprints names it (ClaudeTools.BlueprintPath): the entry with that name
/// in the file named after it wins over a copy, a name with no entry is its own file when there is one, and a name with
/// neither is none.
/// </summary>
[Collection("World files")]
public sealed class ClaudeToolsBlueprintPathTests : WorldsFolder
{
    public ClaudeToolsBlueprintPathTests() : base("vwe-blueprint-path-")
    {
    }

    [Fact]
    public void ABlueprintInTheLibraryIsFoundByItsName()
    {
        string path = BlueprintFiles.Hut(Dir, "Hut");

        Assert.Equal(Path.GetFileName(path), Path.GetFileName(ClaudeTools.BlueprintPath(Dir, "Hut")));
    }

    [Fact]
    public void TheFileNamedAfterTheBlueprintBeatsACopyOfIt()
    {
        string path = BlueprintFiles.Hut(Dir, "Hut");
        File.Copy(path, Path.Combine(Dir, "Hut (2).blueprint"));

        Assert.Equal(Homestead.FileName("Hut"), Path.GetFileName(ClaudeTools.BlueprintPath(Dir, "Hut")));
    }

    [Fact]
    public void ANameWithNoEntryIsNone()
    {
        BlueprintFiles.Hut(Dir, "Hut");

        Assert.Null(ClaudeTools.BlueprintPath(Dir, "Missing"));
    }

    [Fact]
    public void AFileNamedAfterTheBlueprintIsFoundEvenWithoutAnEntry()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, Homestead.FileName("Ghost")), "not a blueprint");

        Assert.Equal(Homestead.FileName("Ghost"), Path.GetFileName(ClaudeTools.BlueprintPath(Dir, "Ghost")));
    }
}
