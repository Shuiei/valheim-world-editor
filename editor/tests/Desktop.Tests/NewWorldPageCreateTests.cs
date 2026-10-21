using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

using static TerrainEditor.Desktop.Tests.NewWorldPageTests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The checks the New world page makes before it creates a world, and what the Create and Start buttons do.
/// </summary>
public class NewWorldPageCreateTests
{
    // Check: a name that cannot be a file name, a seed not of 1 to 10 characters, a world of that name there already
    // (as a folder or as the game's .fwl) are refused; a new name and seed are not.
    [Fact]
    public void CheckRefusesBadNamesSeedsAndWorldsThere()
    {
        string root = Path.Combine(Path.GetTempPath(), "vwe-check-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, "Home"));
        File.WriteAllText(Path.Combine(root, "Hall.fwl"), "");
        try
        {
            // The same names on every system (Linux alone would take "My:World?" and "CON").
            foreach (string bad in new[] { "", "a/b", "My:World?", "CON" })
            {
                Assert.StartsWith("Give the world a name that can be a file name", NewWorldPage.Check(root, bad, "abc"));
            }
            Assert.Equal("The seed is 1 to 10 characters, as in the game.", NewWorldPage.Check(root, "New", ""));
            Assert.Equal("The seed is 1 to 10 characters, as in the game.", NewWorldPage.Check(root, "New", "12345678901"));
            Assert.StartsWith("There is a world called Home already in ", NewWorldPage.Check(root, "Home", "abc"));
            Assert.StartsWith("There is a world called Hall already in ", NewWorldPage.Check(root, "Hall", "abc"));
            Assert.Null(NewWorldPage.Check(root, "New", "abc"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // Create: what Check says is shown and nothing is made; a new name is asked for, trimmed, in the folder chosen.
    [AvaloniaFact]
    public void CreateShowsWhatIsWrongOrAsksForTheWorld()
    {
        using var r = new NewWorldPageRun();
        string cloud = Path.Combine(r.Root, "cloud");
        r.P.SeedBox.Text = " abc ";
        r.P.NameBox.Text = "a/b";
        Click(r.P.Create);
        Assert.StartsWith("Give the world a name that can be a file name", r.P.Problem.Text);
        Assert.Empty(r.Created);
        r.P.Where.SelectedIndex = 1;
        r.P.NameBox.Text = " New home ";
        r.P.AskCreate();
        Assert.Equal("", r.P.Problem.Text);
        Assert.Equal((Path.Combine(cloud, "New home"), "New home", "abc"), Assert.Single(r.Created));
    }

    [AvaloniaFact]
    public void TheStartButtonStopsWhatRunsAndGoesBack()
    {
        using var r = new NewWorldPageRun();
        Click(r.P.Find);
        Assert.False(r.P.Find.IsEnabled);
        Click(ButtonOf(r, "Start"));
        Assert.Equal(1, r.Back);
        Assert.False(r.P.Loading.IsVisible);
        // The world folders offered: the ones given, ~ for the home folder.
        Assert.Equal(2, ((List<string>)r.P.Where.Tag!).Count);
    }

    [AvaloniaFact]
    public void WithoutWorldFoldersTheInstalledGamesIsOffered()
    {
        using var page = new NewWorldPage([], previewSize: 32);
        var roots = (List<string>)page.Where.Tag!;
        Assert.Equal(Places.GameWorldRoot(AppSettings.Load()), Assert.Single(roots));
        Assert.Equal(0, page.Where.SelectedIndex);
        Assert.Equal(10, page.SeedBox.Text!.Length);
    }

    [AvaloniaFact]
    public void CreateWithEmptyBoxesOrNoListenerAsksNothing()
    {
        string root = Path.Combine(Path.GetTempPath(), "vwe-newworld-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            using var page = new NewWorldPage([root], previewSize: 32);
            page.NameBox.Text = null;
            page.SeedBox.Text = null;
            page.AskCreate();
            Assert.StartsWith("Give the world a name that can be a file name", page.Problem.Text);
            // A good world asked for with nobody listening: nothing happens, no problem said.
            page.NameBox.Text = "Fresh";
            page.SeedBox.Text = "abc";
            page.AskCreate();
            Assert.Equal("", page.Problem.Text);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
