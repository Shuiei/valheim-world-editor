using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The mods' settings files the extensions ask for (ModSettings): a setting by its section and name, read from the
/// first BepInEx folder that has the file, and for a live world from the game's own through its bridge (an older or a
/// failing bridge leaves the local ones).
/// </summary>
public sealed class ModSettingsTests : IDisposable
{
    private const string File = "sam.tunnels.cfg";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-modsettings-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Settings naming the test's two BepInEx folders, "first" then "second".</summary>
    private readonly AppSettings _settings;

    public ModSettingsTests()
    {
        _settings = new AppSettings { BepInExFolders = [Bep("first"), Bep("second")] };
    }

    public void Dispose()
    {
        ModSettings.Refresh(new AppSettings(), []);
        Directory.Delete(_dir, true);
    }

    /// <summary>A BepInEx folder of the test's, with its config folder.</summary>
    private string Bep(string name)
    {
        string bep = Path.Combine(_dir, name, "BepInEx");
        Directory.CreateDirectory(Path.Combine(bep, "config"));
        return bep;
    }

    /// <summary>Writes the settings file into one of the test's BepInEx folders.</summary>
    private void Write(string folder, string text) => System.IO.File.WriteAllText(Path.Combine(_dir, folder, "BepInEx", "config", File), text);

    [Fact]
    public void ASettingIsReadByItsSectionAndName()
    {
        var values = ModSettings.Parse("﻿Loose = 1\n## Settings file\n[Building]\n\n# Setting type: Single\nMaxHeight = 32\n[Biomes]\nMaxHeight = 5\n  Lava = false  \n[Building]\nMaxHeight = 40\r\n");
        Assert.Equal("1", values[("", "Loose")]);
        // The last of two the same wins; the same name in another section is another setting.
        Assert.Equal("40", values[("Building", "MaxHeight")]);
        Assert.Equal("5", values[("Biomes", "MaxHeight")]);
        Assert.Equal("false", values[("Biomes", "Lava")]);
        Assert.Equal(4, values.Count);
    }

    [Fact]
    public void TheFirstBepInExFolderWithTheFileIsRead()
    {
        Write("second", "[Building]\nMaxHeight = 32\n");
        ModSettings.Refresh(_settings, [File]);
        Assert.Equal("32", ModSettings.Value(File, "Building", "MaxHeight"));
        Assert.Null(ModSettings.Value(File, "Building", "Other"));
        Assert.Null(ModSettings.Value("other.cfg", "Building", "MaxHeight"));
        Write("first", "[Building]\nMaxHeight = 8\n");
        ModSettings.Refresh(_settings, [File]);
        Assert.Equal("8", ModSettings.Value(File, "Building", "MaxHeight"));
    }

    [Fact]
    public async Task ALiveGamesOwnSettingsReplaceTheLocalOnes()
    {
        Write("first", "[Building]\nMaxHeight = 8\n");
        ModSettings.Refresh(_settings, [File, "missing.cfg"]);
        using var game = new FakeGame { Plugins = [] };
        game.AddConfig(File, "[Building]\nMaxHeight = 96\n");
        using var live = new LiveBridge(game.Url, game.Token);
        await ModSettings.RefreshLive(live, [File, "missing.cfg"]);
        Assert.Equal("96", ModSettings.Value(File, "Building", "MaxHeight"));
        Assert.Null(ModSettings.Value("missing.cfg", "Building", "MaxHeight"));
    }

    [Fact]
    public async Task AnOlderOrFailingBridgeLeavesTheLocalSettings()
    {
        Write("first", "[Building]\nMaxHeight = 8\n");
        ModSettings.Refresh(_settings, [File]);
        using (var older = new FakeGame())
        using (var live = new LiveBridge(older.Url, older.Token))
        {
            await ModSettings.RefreshLive(live, [File]);
        }
        Assert.Equal("8", ModSettings.Value(File, "Building", "MaxHeight"));
        // A file the bridge refuses keeps its local settings; the others are still the game's.
        ModSettings.Refresh(_settings, [File, "other.cfg"]);
        using var failing = new FakeGame { Plugins = [] };
        failing.AddConfig(File, "[Building]\nMaxHeight = 96\n");
        failing.AddConfig("other.cfg", "[Biomes]\nLava = false\n");
        failing.RefusedConfigFiles.Add(File);
        using var refused = new LiveBridge(failing.Url, failing.Token);
        await ModSettings.RefreshLive(refused, [File, "other.cfg"]);
        Assert.Equal("8", ModSettings.Value(File, "Building", "MaxHeight"));
        Assert.Equal("false", ModSettings.Value("other.cfg", "Biomes", "Lava"));
    }
}
