using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The inspector's words for the object data: the plain label of each data name players meet most (its catalogue text),
/// none for any other name, and what each section of the data holds.
/// </summary>
public sealed class InspectorPanelLabelsTests
{
    /// <summary>The data names with a plain label, and the catalogue key of each.</summary>
    private static readonly Dictionary<string, string> Labelled = new()
    {
        ["text"] = "text",
        ["tag"] = "tag",
        ["creator"] = "creator",
        ["creatorName"] = "creatorName",
        ["author"] = "author",
        ["health"] = "health",
        ["plantTime"] = "plantTime",
        ["items"] = "items",
        ["permitted"] = "permitted",
        ["enabled"] = "enabled",
        ["item"] = "item",
        ["variant"] = "variant",
        ["quality"] = "quality",
        ["ownerName"] = "ownerName",
        ["owner"] = "owner",
        ["fuel"] = "fuel",
        ["StartTime"] = "startTime",
        ["level"] = "level",
        ["tamed"] = "tamed",
        ["support"] = "support",
        ["scale"] = "scale",
        ["picked"] = "picked",
        ["picked_time"] = "pickedTime",
        ["spawntime"] = "spawnTime",
        ["lastTime"] = "lastTime",
        ["accTime"] = "accTime",
        ["queued"] = "queued",
        ["state"] = "state",
    };

    [Fact]
    public void TheDataNamesPlayersMeetMostHaveAPlainLabelAndTheOthersNone()
    {
        foreach (var (name, key) in Labelled)
        {
            string label = Lang.T("inspect.data." + key);
            Assert.False(string.IsNullOrWhiteSpace(label), name);
            Assert.Equal(label, InspectorPanel.PlainLabel(name));
        }
        // Case matters: the game's own spelling is the name.
        Assert.Null(InspectorPanel.PlainLabel("Text"));
        Assert.Null(InspectorPanel.PlainLabel("ExampleVehicles_Id"));
        Assert.Null(InspectorPanel.PlainLabel(""));
    }

    [Theory]
    [InlineData("strings", "strings")]
    [InlineData("ints", "ints")]
    [InlineData("floats", "floats")]
    [InlineData("longs", "longs")]
    [InlineData("vec3", "vec3")]
    [InlineData("quats", "quats")]
    [InlineData("bytes", "data")]
    [InlineData("anything else", "data")]
    public void EachSectionSaysWhatItHolds(string section, string key)
    {
        Assert.Equal(Lang.T("inspect.section." + key), InspectorPanel.SectionName(section));
    }
}
