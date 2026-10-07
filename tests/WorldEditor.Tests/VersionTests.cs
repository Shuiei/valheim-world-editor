using System.Text.Json;
using System.Text.RegularExpressions;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

// One version for the editor, the plugin and the packages: the VERSION file.
public class VersionTests
{
	private static readonly string Repo = Path.GetFullPath(Path.Combine(Fixtures.Root, "..", ".."));

	private static string Version => File.ReadAllText(Path.Combine(Repo, "VERSION")).Trim();

	[Fact]
	public void VersionIsMajorMinorPatch() => Assert.Matches(@"^\d+\.\d+\.\d+$", Version);

	[Fact]
	public void TheEditorHasTheVersionOfTheVersionFile() => Assert.Equal(Version, AppHost.Version);

	[Fact]
	public void TheChangelogHasASectionForIt()
	{
		// The Thunderstore package's changelog is made from this one (tools/thunderstore/changelog.py).
		Assert.Matches(new Regex($@"^## v{Regex.Escape(Version)}\b", RegexOptions.Multiline), File.ReadAllText(Path.Combine(Repo, "CHANGELOG.md")));
	}

	[Theory]
	[InlineData("bridge")]
	public void ThunderstoreManifestsFollowTheRules(string package)
	{
		using var m = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo, "tools", "thunderstore", package, "manifest.json")));
		Assert.Equal("@VERSION@", m.RootElement.GetProperty("version_number").GetString());
		Assert.Matches("^[A-Za-z0-9_]{1,128}$", m.RootElement.GetProperty("name").GetString());
		Assert.True(m.RootElement.GetProperty("description").GetString()!.Length <= 250);
		Assert.All(m.RootElement.GetProperty("dependencies").EnumerateArray(), d => Assert.Matches(@"^\w+-\w+-\d+\.\d+\.\d+$", d.GetString()!.Replace("@VERSION@", Version)));
	}

	[Theory]
	[InlineData("/home/u/.config/r2modmanPlus-local/Valheim/profiles/Default/BepInEx/plugins/Team-ValheimWorldEditor_Linux/ValheimWorldEditor", "/home/u/.config/r2modmanPlus-local/Valheim/profiles/Default/BepInEx")]
	[InlineData("/home/u/Downloads/ValheimWorldEditor", null)]
	public void FindsTheModManagerProfileItIsInstalledIn(string appDir, string? bepInEx) =>
		Assert.Equal(bepInEx, LocalGame.InstalledIn(appDir));
}
