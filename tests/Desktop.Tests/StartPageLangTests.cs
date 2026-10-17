using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The start page in another language (the made-up one: PseudoLang): every page (Home with its tiles, My game, the
/// saved servers and the form, the saved worlds, the Workshop's blueprints, a new world) shows only catalogue texts,
/// but for the data (world, server and blueprint names, addresses, folders); forgetting a server asks in the language,
/// and the form's error is said in it. Homestead's folder, the saved servers and the settings are the test's own; it
/// runs alone.
/// </summary>
[Collection("DataDir")]
public sealed class StartPageLangTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-start-lang-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _folder = Homestead.FolderOverride, _servers = ServerConfig.PathOverride, _settings = AppSettings.PathOverride;
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;

    public StartPageLangTests()
    {
        Directory.CreateDirectory(_dir);
        Homestead.FolderOverride = Path.Combine(_dir, "blueprints");
        ServerConfig.PathOverride = Path.Combine(_dir, "servers.cfg");
        AppSettings.PathOverride = Path.Combine(_dir, "settings.json");
        // No game on this computer: My game shows how to install BepInEx and the plugin.
        LocalGame.SearchDefaultPlaces = false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Homestead.FolderOverride = _folder;
        ServerConfig.PathOverride = _servers;
        AppSettings.PathOverride = _settings;
        LocalGame.SearchDefaultPlaces = _defaults;
        Directory.Delete(_dir, recursive: true);
    }

    /// <summary>Clicks the button and runs the posted work it starts.</summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task StartPageShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        // Saved today and weeks ago: the tiles say when in words, and as a date.
        StartPageBlueprintsTests.Blueprint("Hut", 0);
        StartPageBlueprintsTests.Blueprint("Longhouse", 20);
        ServerConfig.Remember(new ServerConfig.Server { Name = "Valhalla", Host = "203.0.113.10", User = "valheim", Password = "pw", Token = "t" });
        ServerConfig.Remember(new ServerConfig.Server { Name = "Bare", Host = "203.0.113.11", User = "admin", KeyFile = "/keys/id" });
        string world = Path.Combine(_dir, "Midgard");
        Directory.CreateDirectory(world);
        var settings = new AppSettings();
        settings.AddRecent(world, "Midgard");
        var page = new StartPage(settings)
        {
            FindWorlds = _ =>
            [
                new Worlds.Info("CITest", "/worlds/CITest", DateTime.Now, 2, "local", true, null),
                new Worlds.Info("Broken", "/nowhere", DateTime.Now, 0, "yours", false, "No save files in it."),
            ],
        };
        // The game's look is there (the card's own states are GameLookBannerLangTests').
        page.LookBanner.Read = () => new GameLook.Snapshot("ready", null, null, null, 1);
        var w = new Window { Width = 1400, Height = 1000, Content = page.View };
        w.Show();
        try
        {
            await page.FillHome();
            await page.PollGame();
            foreach (string mode in new[] { "game", "server", "offline", "workshop", "new" })
            {
                page.SetMode(mode);
            }
            await page.WorldsCheck!;
            await page.FillBlueprints();
            page.ShowHomestead(new Homestead.Status(true, "1.4.0", ["Steam"], "/somewhere"));
            Dispatcher.UIThread.RunJobs();

            string[] data =
            [
                "Valheim", "World Editor", "Hut", "Longhouse", "Valhalla", "Bare", "valheim@203.0.113.10", "admin@203.0.113.11",
                "/keys/id", "Midgard", Ui.Tilde(world), "/worlds/CITest", "/nowhere", "CITest", "Broken", "No save files in it.",
            ];
            Assert.Empty(PseudoLang.Untranslated(page.View, data));
            page.ShowHomestead(new Homestead.Status(false, null, [], "/somewhere"));
            Assert.Empty(PseudoLang.Untranslated(page.WorkshopPanel));

            // Forgetting a server asks in the language in use.
            var asked = new List<string>();
            page.Confirm = text => { asked.Add(text); return Task.FromResult(false); };
            Click(page.SavedServers.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == Lang.T("start.label.forget")));
            Assert.True(PseudoLang.Translated(Assert.Single(asked), []));

            // The form's error, said in the language in use.
            await page.ConnectNew();
            Assert.Equal(PseudoLang.Wrap("Enter the server address."), page.ServerError.Text);
        }
        finally
        {
            page.Stop();
            w.Close();
        }
    }

    // A page shown after the language changed: its title and text are in the language in use then, not the one the
    // start page was made in; back in English they are the English words, as before the catalogue.
    [AvaloniaFact]
    public void APageShownAfterALanguageChangeIsInTheNewLanguage()
    {
        var page = new StartPage(new AppSettings());
        var w = new Window { Width = 1400, Height = 1000, Content = page.View };
        w.Show();
        try
        {
            using (new PseudoLang())
            {
                page.SetMode("server");
                var shown = Texts(page);
                Assert.Contains(PseudoLang.Wrap(Lang.English["start.page.server"]), shown);
                Assert.Contains(PseudoLang.Wrap(Lang.English["start.page.serverText"]), shown);
            }
            page.SetMode("workshop");
            Assert.Contains(Texts(page), t => t?.StartsWith("Build on a blank plot with every piece the game has", StringComparison.Ordinal) == true);
        }
        finally
        {
            page.Stop();
            w.Close();
        }
    }

    /// <summary>Every text block's text on the start page.</summary>
    private static List<string?> Texts(StartPage page) => [.. page.View.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)];
}
