using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tips catalogue: every tip is used by a control, none is empty, and each kind, overlay and area action has one.
/// </summary>
[Collection("World files")]
public class TipsCatalogueTests
{
    [Fact]
    public void EveryTextIsUsedAndNoneIsEmpty()
    {
        string desktop = Path.Combine(Fixtures.Root, "..", "..", "Desktop");
        string code = string.Join("\n", Directory.GetFiles(desktop, "*.cs").Where(f => !f.EndsWith("Tips.cs", StringComparison.Ordinal)).Select(File.ReadAllText));
        // Keys made in code: the area actions, the area's moves, the start page's choices.
        static bool Made(string k) => k.StartsWith("areaAct.", StringComparison.Ordinal) || k is "top.west" or "top.north" or "top.south" or "top.east" or "start.home" or "start.game" or "start.server" or "start.offline" || k.StartsWith("overlay.", StringComparison.Ordinal);
        var unused = Tips.Texts.Keys.Where(k => !Made(k) && !code.Contains($"\"{k}\"")).ToList();
        Assert.True(unused.Count == 0, "Tips no control uses: " + string.Join(", ", unused));
        Assert.All(Tips.Texts, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), kv.Key));
        Assert.All(Enum.GetValues<AreaPanel.Act>(), act => Assert.False(string.IsNullOrWhiteSpace(Tips.Of($"areaAct.{act}"))));
        Assert.All(ObjectKinds.All, k => Assert.EndsWith(".", Tips.Kind(k)));
        Assert.All(Overlays.All, l => Assert.EndsWith(".", Tips.Overlay(l)));
        Assert.Throws<KeyNotFoundException>(() => Tips.Of("no.such.tip"));
    }
}
