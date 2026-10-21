using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Settings window in another language (the made-up one: PseudoLang), with a mod's extension and an add-on listed:
/// it shows only catalogue texts, but for the data it lists (the mod and its file, the languages' own names).
/// </summary>
[Collection("DataDir")]
public sealed class SettingsDialogLangTests
{
    [AvaloniaFact]
    public async Task SettingsShowOnlyCatalogueTexts()
    {
        string root = Path.Combine(Path.GetTempPath(), "vwe-settings-lang-" + Guid.NewGuid().ToString("N")[..8]);
        bool defaults = LocalGame.SearchDefaultPlaces;
        LocalGame.SearchDefaultPlaces = false;
        try
        {
            string mod = Path.Combine(root, "BepInEx", "plugins", "SomeMod");
            Directory.CreateDirectory(mod);
            File.WriteAllText(Path.Combine(mod, ModManifests.FileName), """{ "version": 1, "extension": "SomeMod.WorldEditor.dll" }""");
            File.WriteAllText(Path.Combine(mod, "SomeMod.WorldEditor.dll"), "not a DLL");
            AddOns.FolderForTests = root;
            string addOn = Path.Combine(root, AddOns.FolderName, "Digging");
            Directory.CreateDirectory(addOn);
            File.WriteAllText(Path.Combine(addOn, ModManifests.FileName), """{ "version": 1, "serves": "DigMod.dll" }""");
            var settings = new AppSettings { BepInExFolders = { Path.Combine(root, "BepInEx") } };
            var owner = new Window { Width = 900, Height = 700 };
            owner.Show();
            using var pseudo = new PseudoLang();

            var shown = SettingsDialog.Show(owner, settings);
            var d = TestWindows.Shown(owner);
            // Claude's connection status (when a test left it on) is converted with Claude's texts.
            Assert.Empty(PseudoLang.Untranslated(d, "SomeMod · SomeMod.WorldEditor.dll", Lang.English[Lang.NameKey], ClaudeServer.Status ?? ""));

            var cancel = d.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == Lang.T("common.cancel"));
            cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(await shown);
        }
        finally
        {
            LocalGame.SearchDefaultPlaces = defaults;
            AddOns.FolderForTests = null;
            ModExtensions.Forget();
            ModManifests.Forget();
            Directory.Delete(root, true);
        }
    }
}
