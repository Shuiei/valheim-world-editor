using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The sections a blueprint file's "#" lines start (BlueprintFormats.SectionAfter): the three PlanBuild sections, and any
/// other mod's section, which is skipped.
/// </summary>
public class BlueprintFormatsSectionTests
{
    [Theory]
    [InlineData("#SnapPoints", "snap")]
    [InlineData("#Terrain", "terrain")]
    [InlineData("#Pieces", "pieces")]
    [InlineData("#TerrainHeight", "skip")]
    public void AHashLineStartsItsSection(string line, string section) => Assert.Equal(section, BlueprintFormats.SectionAfter(line));
}
