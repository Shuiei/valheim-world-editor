using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Mountain panel says when a slider or the seed is moved by hand, and whether the buried trees go.
/// </summary>
public class MountainPanelBranchesTests
{
    [AvaloniaFact]
    public void MovingASliderOrTheSeedIsAChange()
    {
        var p = new MountainPanel();
        int changed = 0;
        p.Changed += () => changed++;
        p.HeightSlider.Value = p.HeightSlider.Value == 100 ? 120 : 100;
        Assert.True(changed > 0);
        int before = changed;
        p.SeedBox.Value = (p.SeedBox.Value ?? 0) + 1;
        Assert.True(changed > before);
        Assert.True(p.Clear);
        p.ClearBox.IsChecked = false;
        Assert.False(p.Clear);
    }
}
