using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;
using static TerrainEditor.Desktop.Tests.SettingsDialogRun;
using static TerrainEditor.Desktop.Tests.SharedButtons;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Settings window's choices: Claude's connection (its port and the command, its token), what draws on Windows, the
/// language among the catalogues there are, and a mod in a BepInEx folder offering an extension, turned on and saved with
/// its file's hash. It saves to the tests' settings file, so it runs alone.
/// </summary>
[Collection("DataDir")]
public class SettingsChoicesTests
{
    // Claude's connection: off at first (its details hidden); on, the port and the command to add the editor
    // to Claude Code (with the token); a port that is not one is refused; saved with the token.
    [AvaloniaFact]
    public async Task ClaudesConnectionIsChosenWithItsPortAndToken()
    {
        using var t = new Temp();
        var settings = new AppSettings();
        var owner = Owner();
        var shown = SettingsDialog.Show(owner, settings);
        var d = TestWindows.Shown(owner);
        var allow = d.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content as string == "Allow Claude to connect");
        var scripts = d.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content as string == "Claude may run scripts");
        var port = Named(d, "ClaudePort");
        var command = Named(d, "ClaudeCommand");
        Assert.False(allow.IsChecked);
        Assert.False(port.IsEffectivelyVisible);
        // Scripts: off at first, with the other details; the user's leave once.
        Assert.False(scripts.IsChecked);
        Assert.False(scripts.IsEffectivelyVisible);
        allow.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(port.IsEffectivelyVisible);
        Assert.True(scripts.IsEffectivelyVisible);
        scripts.IsChecked = true;
        Assert.StartsWith("claude mcp add --transport http valheim-editor http://127.0.0.1:5731/mcp --header \"Authorization: Bearer ", command.Text);
        port.Text = "80";
        Click(Buttons(d, "Save").Single());
        Assert.Contains(Texts(d).Split(" | "), x => x.Contains("1024 to 65535"));
        port.Text = "6000";
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("127.0.0.1:6000/mcp", command.Text);
        string token = command.Text!.Split("Bearer ")[1].TrimEnd('"');
        Click(Buttons(d, "Save").Single());
        Assert.True(await shown);
        var kept = AppSettings.Load();
        Assert.True(kept.ClaudeConnect);
        Assert.True(kept.ClaudeScripts);
        Assert.Equal(6000, kept.ClaudePort);
        Assert.Equal("127.0.0.1", kept.ClaudeAddress);
        Assert.Equal(token, kept.ClaudeToken);
    }

    // The address: picked from a list that says each in words (this computer only, every network, then this computer's
    // networks) or typed; the saved one shown as its choice; a name or nonsense is refused; another address shows in
    // the command; every network shows this computer's 127.0.0.1 there, and under it the address another computer uses
    // (this computer's name); saved as the address alone.
    [AvaloniaFact]
    public async Task ClaudesAddressIsChosenAndChecked()
    {
        using var t = new Temp();
        var settings = new AppSettings { ClaudeConnect = true };
        var owner = Owner();
        var shown = SettingsDialog.Show(owner, settings);
        var d = TestWindows.Shown(owner);
        var address = d.GetLogicalDescendants().OfType<ComboBox>().Single(b => b.Name == "ClaudeAddress");
        var command = Named(d, "ClaudeCommand");
        var remote = d.GetLogicalDescendants().OfType<TextBlock>().Single(b => b.Name == "ClaudeRemote");
        var unencrypted = d.GetLogicalDescendants().OfType<TextBlock>().Single(b => b.Name == "ClaudeUnencrypted");
        var choices = address.Items.Cast<string>().ToList();
        Assert.Equal(["127.0.0.1 (this computer only)", "0.0.0.0 (every network)"], choices.Take(2));
        Assert.All(choices.Skip(2), c => Assert.Matches(@"^\d+\.\d+\.\d+\.\d+ \(.+\)$", c));
        Assert.Equal("127.0.0.1 (this computer only)", address.Text);
        Assert.False(remote.IsVisible);
        Assert.False(unencrypted.IsVisible);
        address.Text = "my-pc.local";
        Click(Buttons(d, "Save").Single());
        Assert.Contains(Texts(d).Split(" | "), x => x.Contains("must be an IP address"));
        Assert.Single(owner.OwnedWindows);
        // Off this computer: said, where the address is chosen, that the connection is not encrypted.
        address.Text = "192.0.2.20";
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(ClaudeToolsTransportTests.McpUrl("192.0.2.20:5731"), command.Text);
        Assert.False(remote.IsVisible);
        Assert.True(unencrypted.IsVisible);
        Assert.Contains("not encrypted", unencrypted.Text);
        // Another loopback address: still this computer only.
        address.Text = "::1";
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(ClaudeToolsTransportTests.McpUrl("[::1]:5731"), command.Text);
        Assert.False(unencrypted.IsVisible);
        address.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("0.0.0.0 (every network)", address.Text);
        Assert.Contains("http://127.0.0.1:5731/mcp", command.Text);
        Assert.True(remote.IsVisible);
        Assert.True(unencrypted.IsVisible);
        Assert.Contains(ClaudeServer.RemoteUrl(5731), remote.Text);
        // And by address, for networks that do not answer the name: each of this computer's, ready to use.
        Assert.All(ClaudeServer.RemoteAddressUrls(5731), u => Assert.Contains(u, remote.Text));
        Click(Buttons(d, "Save").Single());
        Assert.True(await shown);
        Assert.Equal("0.0.0.0", AppSettings.Load().ClaudeAddress);

        // Opened again: the saved address shown as its choice.
        var again = SettingsDialog.Show(owner, AppSettings.Load());
        var d2 = TestWindows.Shown(owner);
        Assert.Equal("0.0.0.0 (every network)", d2.GetLogicalDescendants().OfType<ComboBox>().Single(b => b.Name == "ClaudeAddress").Text);
        Click(Buttons(d2, "Cancel").Single());
        Assert.False(await again);
    }

    // Drawing: only on Windows; the driver's OpenGL off at first, saved when chosen.
    [AvaloniaFact]
    public async Task WindowsChoosesWhatDraws()
    {
        using var t = new Temp();
        bool was = SettingsDialog.OnWindows;
        try
        {
            static CheckBox DriverGl(Window d) => d.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content as string == "Draw with the graphics driver's OpenGL");
            var owner = Owner();
            SettingsDialog.OnWindows = false;
            var shown = SettingsDialog.Show(owner, new AppSettings());
            var d = TestWindows.Shown(owner);
            Assert.False(DriverGl(d).IsEffectivelyVisible);
            Click(Buttons(d, "Cancel").Single());
            Assert.False(await shown);
            SettingsDialog.OnWindows = true;
            shown = SettingsDialog.Show(owner, new AppSettings());
            d = TestWindows.Shown(owner);
            Assert.True(DriverGl(d).IsEffectivelyVisible);
            Assert.False(DriverGl(d).IsChecked);
            DriverGl(d).IsChecked = true;
            Click(Buttons(d, "Save").Single());
            Assert.True(await shown);
            Assert.True(AppSettings.Load().DriverOpenGl);
        }
        finally
        {
            SettingsDialog.OnWindows = was;
        }
    }

    // Language: the computer's at first, then English and the catalogues there are (one dropped in the lang folder
    // listed by its own name); the one chosen saved by its code, and shown chosen again.
    [AvaloniaFact]
    public async Task TheLanguageIsChosenAmongTheCatalogues()
    {
        using var t = new Temp();
        string? was = Lang.FolderOverride;
        Lang.FolderOverride = t.Dir("lang");
        try
        {
            File.WriteAllText(Path.Combine(Lang.Folder, "xx.json"), """{ "lang.name": "Testish" }""");
            static ComboBox Language(Window d) => d.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "Language");
            var owner = Owner();
            var settings = new AppSettings();
            var shown = SettingsDialog.Show(owner, settings);
            var d = TestWindows.Shown(owner);
            Assert.Equal(["The computer's language", "English", "Testish"], Language(d).Items.Cast<string>());
            Assert.Equal(0, Language(d).SelectedIndex);
            Language(d).SelectedIndex = 2;
            Click(Buttons(d, "Save").Single());
            Assert.True(await shown);
            Assert.Equal("xx", AppSettings.Load().Language);
            shown = SettingsDialog.Show(owner, settings);
            d = TestWindows.Shown(owner);
            Assert.Equal(2, Language(d).SelectedIndex);
            Language(d).SelectedIndex = 0;
            Click(Buttons(d, "Save").Single());
            Assert.True(await shown);
            Assert.Null(AppSettings.Load().Language);
        }
        finally
        {
            Lang.FolderOverride = was;
        }
    }

    // A mod in a BepInEx folder offering an extension (here a file that is no DLL): listed off, turned on and saved
    // with its file's hash, then shown as not running, with why.
    [AvaloniaFact]
    public async Task AModsExtensionIsTurnedOnAndSavedWithItsFile()
    {
        using var t = new Temp();
        bool defaults = LocalGame.SearchDefaultPlaces;
        LocalGame.SearchDefaultPlaces = false;
        try
        {
            string mod = t.Dir("BepInEx/plugins/SomeMod");
            File.WriteAllText(Path.Combine(mod, ModManifests.FileName), """{ "version": 1, "extension": "SomeMod.WorldEditor.dll" }""");
            string dll = Path.Combine(mod, "SomeMod.WorldEditor.dll");
            File.WriteAllText(dll, "not a DLL");
            var settings = new AppSettings { BepInExFolders = { Path.Combine(t.Root, "BepInEx") } };
            var owner = Owner();
            var shown = SettingsDialog.Show(owner, settings);
            var d = TestWindows.Shown(owner);
            var box = d.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content as string == "SomeMod · SomeMod.WorldEditor.dll");
            Assert.False(box.IsChecked);

            box.IsChecked = true;
            Click(Buttons(d, "Save").Single());

            Assert.True(await shown);
            Assert.Equal(ModExtensions.Sha256Of(dll), AppSettings.Load().Extensions[dll]);
            Assert.Equal(ModExtensions.Status.Failed, Assert.Single(ModExtensions.Offered).State);
            // Shown again: on, and why it does not run.
            shown = SettingsDialog.Show(owner, settings);
            d = TestWindows.Shown(owner);
            Assert.True(d.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content as string == "SomeMod · SomeMod.WorldEditor.dll").IsChecked);
            Assert.Contains("Not running: ", Texts(d));
            Click(Buttons(d, "Cancel").Single());
            Assert.False(await shown);
        }
        finally
        {
            LocalGame.SearchDefaultPlaces = defaults;
            ModExtensions.Forget();
            ModManifests.Forget();
        }
    }
}
