using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The random seed search (SeedPreview.Search): how many seeds it keeps, their order, and what it hands back.
/// </summary>
public class SeedSearchTests
{
    [Fact]
    public void NoSeedsAskedForGivesNothing()
    {
        Assert.Empty(SeedPreview.Search(new SeedPreview.Wishes(), 0, 3, size: 16, cancel: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheSearchKeepsNoMoreThanAskedFor()
    {
        var found = SeedPreview.Search(new SeedPreview.Wishes(), 3, 2, size: 16, cancel: TestContext.Current.CancellationToken);
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void TheBestPreviewComesFirst()
    {
        var found = SeedPreview.Search(new SeedPreview.Wishes(), 4, 4, size: 16, cancel: TestContext.Current.CancellationToken);
        for (int i = 1; i < found.Count; i++)
        {
            Assert.True(found[i - 1].Score >= found[i].Score);
        }
    }

    [Fact]
    public void FoundPreviewsComeWithoutTheirGeneratorAndLayout()
    {
        var found = SeedPreview.Search(new SeedPreview.Wishes(), 2, 2, size: 16, cancel: TestContext.Current.CancellationToken);
        Assert.All(found, f =>
        {
            Assert.Null(f.Preview.Generator);
            Assert.Null(f.Preview.Layout);
        });
    }
}
