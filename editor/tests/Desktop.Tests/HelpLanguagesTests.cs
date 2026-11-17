using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Markdig.Syntax;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The documentation's translations (a made-up one: FakeDocsLanguage): the language folders, the file shown for a page,
/// a translation's links and pictures read from its folder but leading to the English pages, the English heading ids
/// a translation with the same headings answers to, its source mark, and the contents, search and Help window in the
/// language shown. They change the language in use: the DataDir collection.
/// </summary>
[Collection("DataDir")]
public sealed class HelpLanguagesTests
{
    /// <summary>The made-up translation's folder.</summary>
    private const string Zz = FakeDocsLanguage.Code;

    /// <summary>
    /// The made-up translation: its first page (the contents), the map's page (up to date, with the map's headings),
    /// the Select page (made from another English text, with other headings) and a guide (with no source mark).
    /// </summary>
    /// <param name="take">Whether to take its language.</param>
    private static FakeDocsLanguage Translated(bool take = true) => new(new Dictionary<string, string>
    {
        [Zz + "/README.md"] = "# Aide\n\n## Premiers pas\n\n- [La carte du monde](map.md) : ce qu'elle montre.\n- [Choisir](select.md)\n\n## Guides\n\n- [Sauvegarder](guides/back-up-a-world.md)\n",
        [Zz + "/map.md"] = FakeDocsLanguage.Translation(
            "map.md",
            "Le zorglub de la carte. Voir [la sélection](select.md#titre-2).\n\n![La carte](images/map.jpg)",
            FakeDocsLanguage.Current("map.md")),
        [Zz + "/select.md"] = "<!-- source: 00000000 -->\n\n# Choisir\n\n## Une seule partie\n\nTexte.\n",
        [Zz + "/guides/back-up-a-world.md"] = "# Sauvegarder\n\nVoir [la carte](../map.md#titre-3).\n",
    }, take);

    [Fact]
    public void ALanguageFolderIsATopFolderWithItsOwnFirstPageAndPagesStayEnglish()
    {
        var english = HelpDocs.Pages.ToList();
        using var fake = new FakeDocsLanguage(new Dictionary<string, string>
        {
            [Zz + "/README.md"] = "# Aide",
            [Zz + "/guides/back-up-a-world.md"] = "# Sauvegarder",
            // No first page of its own: not a language.
            ["yy/map.md"] = "# Carte",
        }, take: false);
        Assert.Contains(Zz, HelpDocs.Languages);
        Assert.DoesNotContain("yy", HelpDocs.Languages);
        Assert.Equal(english, HelpDocs.Pages);
        Assert.Equal(["README.md", "guides/back-up-a-world.md"], HelpDocs.Translations(Zz));
        Assert.Equal("guides/x.md", HelpDocs.EnglishPath(Zz + "/guides/x.md"));
        Assert.Equal("images/map.jpg", HelpDocs.EnglishPath(Zz + "/images/map.jpg"));
        Assert.Equal("yy/map.md", HelpDocs.EnglishPath("yy/map.md"));
        Assert.Equal("guides/x.md", HelpDocs.EnglishPath("guides/x.md"));
        Assert.Equal("# Aide", HelpDocs.Text(Zz + "/README.md"));
    }

    [Fact]
    public void APageIsShownTranslatedInTheEditorsLanguageWhenItHasATranslation()
    {
        using (Translated(take: false))
        {
            // English in use: no language shown.
            Assert.Null(HelpDocs.Language);
            Assert.Equal("map.md", HelpDocs.ShownFile("map.md"));
        }
        using (Translated())
        {
            Assert.Equal(Zz, HelpDocs.Language);
            Assert.Equal(Zz + "/map.md", HelpDocs.ShownFile("map.md"));
            Assert.Equal(Zz + "/README.md", HelpDocs.ShownFile(HelpDocs.Home));
            Assert.Equal("area.md", HelpDocs.ShownFile("area.md"));
        }
        Assert.Null(HelpDocs.Language);
        Assert.Equal("map.md", HelpDocs.ShownFile("map.md"));
    }

    [Theory]
    [InlineData(Zz + "/map.md", "select.md#titre-2", "select.md", "titre-2")]
    [InlineData(Zz + "/map.md", "select.md#use-it", "select.md", "use-it")]
    [InlineData(Zz + "/map.md", "#titre-3", "map.md", "titre-3")]
    [InlineData(Zz + "/map.md", "./guides/back-up-a-world.md", "guides/back-up-a-world.md", null)]
    [InlineData(Zz + "/guides/back-up-a-world.md", "../map.md#titre-3", "map.md", "titre-3")]
    [InlineData(Zz + "/guides/back-up-a-world.md", "../../area.md", "area.md", null)]
    public void ALinkOnATranslationLeadsToTheEnglishPageItsFileTranslates(string file, string href, string page, string? anchor)
    {
        using var fake = Translated(take: false);
        var link = HelpDocs.Resolve(file, href);
        Assert.Equal(new HelpTarget(page, anchor), link?.Target);
        Assert.Null(link?.Web);
    }

    [Theory]
    [InlineData("../../README.md#installation", HelpDocs.Repository + "README.md#installation")]
    [InlineData("../../editor/Desktop/ScriptApi.csx", HelpDocs.Repository + "editor/Desktop/ScriptApi.csx")]
    [InlineData("../worldeditor.schema.json", HelpDocs.Repository + "docs/worldeditor.schema.json")]
    [InlineData("../architecture.md#help", HelpDocs.Repository + "docs/architecture.md#help")]
    // Where it really leads from the translation's folder, as on GitHub.
    [InlineData("worldeditor.schema.json", HelpDocs.Repository + "docs/" + Zz + "/worldeditor.schema.json")]
    public void ALinkOnATranslationOutOfThePagesCarriedOpensItsFileOnGitHub(string href, string address)
    {
        using var fake = Translated(take: false);
        var link = HelpDocs.Resolve(Zz + "/map.md", href);
        Assert.Equal(new Uri(address), link?.Web);
        Assert.Null(link?.Target);
    }

    [AvaloniaFact]
    public void ATranslationsPictureIsItsOwnOrElseTheEnglishOneOfTheSameName()
    {
        using var fake = Translated(take: false);
        Assert.Equal("images/map.jpg", HelpDocs.Picture(Zz + "/map.md", "images/map.jpg"));
        Assert.Equal("images/map.jpg", HelpDocs.Picture(Zz + "/map.md", "../images/map.jpg"));
        Assert.Equal("images/map.jpg", HelpDocs.Picture(Zz + "/guides/back-up-a-world.md", "../images/map.jpg"));
        Assert.Equal("images/nowhere.jpg", HelpDocs.Picture(Zz + "/map.md", "images/nowhere.jpg"));
        Assert.Equal("images/map.jpg", HelpDocs.Picture("map.md", "images/map.jpg"));
        // Drawn: the English picture, enlarged by a click.
        Assert.Equal(["enlarge images/map.jpg"], ClickPicture());
        using var s = HelpDocs.Open("images/map.jpg")!;
        using var bytes = new MemoryStream();
        s.CopyTo(bytes);
        fake.Add(Zz + "/images/map.jpg", bytes.ToArray());
        Assert.Equal(Zz + "/images/map.jpg", HelpDocs.Picture(Zz + "/map.md", "images/map.jpg"));
        Assert.Equal("images/map.jpg", HelpDocs.Picture(Zz + "/map.md", "../images/map.jpg"));
        Assert.Equal(["enlarge " + Zz + "/images/map.jpg"], ClickPicture());
    }

    /// <summary>Draws the made-up translation of the map's page and clicks its picture: what was enlarged.</summary>
    private static List<string> ClickPicture()
    {
        var clicks = new List<string>();
        var view = new HelpView(Zz + "/map.md", _ => { }, path => clicks.Add("enlarge " + path));
        var page = view.Build(HelpDocs.Read(Zz + "/map.md"));
        page.GetLogicalDescendants().OfType<Button>().Single().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        return clicks;
    }

    [Fact]
    public void ATranslationWithTheSameHeadingsAnswersToTheEnglishIds()
    {
        using var fake = Translated(take: false);
        var english = HelpDocs.Read("map.md").Descendants<HeadingBlock>().Select(h => HelpText.Id(h)!).ToList();
        var aliases = HelpDocs.AnchorAliases("map.md", Zz + "/map.md");
        Assert.Equal(
            english.Select((id, i) => KeyValuePair.Create(id, "titre-" + (i + 1))).OrderBy(a => a.Key, StringComparer.Ordinal),
            aliases.OrderBy(a => a.Key, StringComparer.Ordinal));
        Assert.Equal("titre-7", aliases["shortcuts"]);
        // Other headings than the English page's: no English ids.
        Assert.Empty(HelpDocs.AnchorAliases("select.md", Zz + "/select.md"));
        Assert.Empty(HelpDocs.AnchorAliases("map.md", "map.md"));
    }

    [Fact]
    public void ATranslationIsOutdatedWhenItsSourceMarkIsNotTheEnglishPagesNow()
    {
        using var fake = Translated(take: false);
        Assert.False(HelpDocs.IsOutdated("map.md", Zz + "/map.md"));
        Assert.True(HelpDocs.IsOutdated("select.md", Zz + "/select.md"));
        Assert.True(HelpDocs.IsOutdated("guides/back-up-a-world.md", Zz + "/guides/back-up-a-world.md"));
        Assert.False(HelpDocs.IsOutdated("map.md", "map.md"));
    }

    [Fact]
    public void TheSourceMarkIsTheStartOfTheEnglishTextsSha256WithItsLineEndsMadeUnixOnes()
    {
        // sha256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad.
        Assert.Equal("ba7816bf", HelpDocs.SourceMark("abc"));
        Assert.Equal(HelpDocs.SourceMark("# Map\n\nText.\n"), HelpDocs.SourceMark("# Map\r\n\r\nText.\r\n"));
        Assert.NotEqual(HelpDocs.SourceMark("# Map\n"), HelpDocs.SourceMark("# Maps\n"));
    }

    [Fact]
    public void TheContentsAndTheSearchAreInTheLanguageShown()
    {
        using (Translated())
        {
            Assert.Equal(["Premiers pas", "Guides"], HelpContents.Sections.Select(s => s.Title));
            Assert.Equal(
                [new HelpEntry("La carte du monde", new HelpTarget("map.md")), new HelpEntry("Choisir", new HelpTarget("select.md"))],
                HelpContents.Sections[0].Entries);
            Assert.Equal(new HelpEntry("Sauvegarder", new HelpTarget("guides/back-up-a-world.md")), HelpContents.Find("guides/back-up-a-world.md"));
            var hit = Assert.Single(HelpSearch.Carried.Find("zorglub"));
            Assert.Equal((new HelpTarget("map.md"), "Titre 1"), (hit.Target, hit.Page));
            // A page with no translation is searched in English.
            Assert.Contains(HelpSearch.Carried.Find("sculpt brushes"), h => h.Target == new HelpTarget("terrain.md", "sculpt-brushes"));
            Assert.DoesNotContain(HelpSearch.Carried.Find("select box"), h => h.Target.Page == "select.md");
        }
        Assert.DoesNotContain(HelpContents.Sections, s => s.Title == "Premiers pas");
        Assert.Equal("Select", HelpContents.Find("select.md")?.Title);
        Assert.Empty(HelpSearch.Carried.Find("zorglub"));
    }

    /// <summary>Runs a test on the Help window opened at a place, then closes it.</summary>
    private static void With(HelpTarget target, Action<HelpWindow> test)
    {
        var w = HelpWindow.Open(target);
        Dispatcher.UIThread.RunJobs();
        try
        {
            test(w);
        }
        finally
        {
            w.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void TheHelpWindowShowsTheTranslationAtAnEnglishPlace()
    {
        using var fake = Translated();
        With(new HelpTarget("map.md", "shortcuts"), w =>
        {
            Assert.Equal(Zz + "/map.md", w.ShownFile);
            Assert.Equal(Lang.T("docs.titleOf", "Titre 1"), w.Title);
            Assert.Same(w.View!.Anchors["titre-7"], w.View.Anchors["shortcuts"]);
            Assert.False(w.Note.IsVisible);
            Assert.False(w.EnglishButton.IsVisible);
            // The contents: the translation's names, the translated part at the English place marked.
            var on = w.Side.Children.OfType<Button>().Where(b => b.Classes.Contains("on")).Select(b => ((TextBlock)b.Content!).Text).ToList();
            Assert.Equal(["La carte du monde", "Titre 7"], on);
            var opened = new List<Uri>();
            w.OpenUrl = u => { opened.Add(u); return Task.FromResult(true); };
            w.BrowserButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal([HelpDocs.OnGitHub(new HelpTarget(Zz + "/map.md", "titre-7"))], opened);
            // A link of the translation: the English page's place, shown translated.
            w.Follow("select.md#titre-2");
            Assert.Equal(new HelpTarget("select.md", "titre-2"), w.Shown);
            Assert.Equal(Zz + "/select.md", w.ShownFile);
        });
    }

    [AvaloniaFact]
    public void AnOutdatedTranslationIsShownWithANoteAndTheEnglishPageOnAsking()
    {
        using var fake = Translated();
        With(new HelpTarget("select.md", "shortcuts"), w =>
        {
            Assert.Equal(Zz + "/select.md", w.ShownFile);
            Assert.True(w.Note.IsVisible);
            Assert.Equal(Lang.T("docs.outdated"), w.Note.Text);
            Assert.True(w.EnglishButton.IsVisible);
            w.EnglishButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("select.md", w.ShownFile);
            Assert.Equal(new HelpTarget("select.md"), w.Shown);
            Assert.StartsWith("Select", w.Title);
            Assert.False(w.Note.IsVisible);
            Assert.False(w.EnglishButton.IsVisible);
            Assert.Contains("shortcuts", w.View!.Anchors.Keys);
            // The English page's links lead from its own folder.
            w.Follow("map.md");
            Assert.Equal(Zz + "/map.md", w.ShownFile);
            w.Move(-1);
            Assert.Equal(Zz + "/select.md", w.ShownFile);
            Assert.True(w.Note.IsVisible);
        });
    }
}
