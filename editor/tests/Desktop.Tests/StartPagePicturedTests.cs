using Avalonia.Media.Imaging;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The blueprints on the start page with their pictures: a picture that is named but not on disk gives no picture (the
/// kind's icon is shown instead), a blueprint with no picture gives none, and the blueprints keep their order.
/// </summary>
public sealed class StartPagePicturedTests
{
    /// <summary>A blueprint entry with a name and a picture path, or none.</summary>
    private static Homestead.Entry Entry(string name, string? picture) =>
        new(Path.Combine(Path.GetTempPath(), name + ".blueprint"), name, null, null, 3, picture, DateTime.Now);

    /// <summary>Calls the start page's picture loader.</summary>
    private static List<(Homestead.Entry Entry, Bitmap? Picture)> Pictured(params Homestead.Entry[] entries) =>
        (List<(Homestead.Entry Entry, Bitmap? Picture)>)PrivateMethods.Call(typeof(StartPage), "Pictured", null, (IEnumerable<Homestead.Entry>)entries)!;

    /// <summary>A blueprint with no picture named has no picture.</summary>
    [Fact]
    public void ABlueprintWithNoPictureNamedHasNoPicture()
    {
        var pictured = Pictured(Entry("plain", null));

        Assert.Null(Assert.Single(pictured).Picture);
    }

    /// <summary>A picture named but not on disk gives no picture, and the blueprint is still listed.</summary>
    [Fact]
    public void AMissingPictureFileGivesNoPictureAndKeepsTheBlueprint()
    {
        var entry = Entry("missing", Path.Combine(Path.GetTempPath(), "vwe-no-picture-" + Guid.NewGuid().ToString("N")[..8] + ".png"));

        var pictured = Pictured(entry);

        Assert.Equal(entry, Assert.Single(pictured).Entry);
        Assert.Null(Assert.Single(pictured).Picture);
    }

    /// <summary>The blueprints come back in the order they were given.</summary>
    [Fact]
    public void TheBlueprintsKeepTheirOrder()
    {
        var first = Entry("first", null);
        var second = Entry("second", null);

        var pictured = Pictured(first, second);

        Assert.Equal([first, second], pictured.Select(p => p.Entry));
    }
}
