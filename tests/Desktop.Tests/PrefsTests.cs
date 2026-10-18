using System.Text.Json.Nodes;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What the editor remembers between runs (Prefs, as the web editor kept it in the browser): the store's edge cases
/// (missing, broken, unwritable files, wrong types, the delayed write).
/// </summary>
public sealed class PrefsTests : PrefsFolder
{
    [Fact]
    public void ValuesOfEveryTypeComeBackFromTheFile()
    {
        var p = Prefs.Load(File1);
        Assert.False(p.Has("a"));
        Assert.Equal(7, p.Get("a", 7));
        p.Set("a", true);
        p.Set("b", "text");
        p.Set("c", new[] { 1.5f, -2f, 3f });
        p.Set("d", new JsonObject { ["x"] = 1 });
        p.Flush();
        var q = Prefs.Load(File1);
        Assert.True(q.Get("a", false));
        Assert.Equal("text", q.Get("b", ""));
        Assert.Equal(new[] { 1.5f, -2f, 3f }, q.Get<float[]?>("c", null));
        Assert.True(q.TryGet("d", out JsonObject? d));
        Assert.Equal(1, (int)d!["x"]!);
        // Another type than the one kept: not there, the fallback.
        Assert.False(q.TryGet("b", out bool _));
        Assert.Equal(5, q.Get("b", 5));
        Assert.False(q.TryGet("a", out float[]? _));
        // Removed: gone from the file too.
        q.Remove("a");
        q.Remove("never there");
        q.Flush();
        Assert.False(Prefs.Load(File1).Has("a"));
        // Written sorted and readable.
        string text = File.ReadAllText(File1);
        Assert.True(text.IndexOf("\"b\"", StringComparison.Ordinal) < text.IndexOf("\"c\"", StringComparison.Ordinal));
        Assert.Contains("\n", text);
    }

    [Fact]
    public void ANullValueIsNotAValue()
    {
        File.WriteAllText(File1, "{\"a\": null, \"b\": 3}");
        var p = Prefs.Load(File1);
        Assert.True(p.Has("a"));
        Assert.False(p.TryGet("a", out string? _));
        Assert.Equal(3, p.Get("b", 0));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("42")]
    [InlineData("")]
    public void ABrokenFileMeansTheDefaults(string content)
    {
        File.WriteAllText(File1, content);
        var p = Prefs.Load(File1);
        Assert.False(p.Has("a"));
        // And it is written anew.
        p.Set("a", 1);
        p.Flush();
        Assert.Equal(1, Prefs.Load(File1).Get("a", 0));
    }

    [Fact]
    public void AFileThatCannotBeWrittenKeepsTheValuesInMemory()
    {
        Directory.CreateDirectory(File1);
        var p = Prefs.Load(File1);
        p.Set("a", 1);
        p.Flush();
        Assert.Equal(1, p.Get("a", 0));
        Assert.True(Directory.Exists(File1));
    }

    [Fact]
    public void AMissingFolderIsMade()
    {
        string path = Path.Combine(Dir, "new", "deeper", "prefs.json");
        var p = Prefs.Load(path);
        p.Set("a", 2);
        p.Flush();
        Assert.Equal(2, Prefs.Load(path).Get("a", 0));
    }

    [Fact]
    public async Task ChangesAreWrittenAMomentAfterTheLastOne()
    {
        var p = Prefs.Load(File1);
        for (int i = 0; i < 20; i++)
        {
            p.Set("n", i);
        }
        Assert.False(File.Exists(File1));
        for (int i = 0; i < 50 && !File.Exists(File1); i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        Assert.Equal(19, Prefs.Load(File1).Get("n", -1));
        // Removing writes too.
        p.Remove("n");
        for (int i = 0; i < 50 && Prefs.Load(File1).Has("n"); i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        Assert.False(Prefs.Load(File1).Has("n"));
    }

    [Fact]
    public void InMemoryPrefsAreNeverWritten()
    {
        var p = Prefs.InMemory();
        p.Set("a", 1);
        p.Remove("a");
        p.Set("b", 2);
        p.Flush();
        Assert.Equal(2, p.Get("b", 0));
        Assert.False(File.Exists(Prefs.DefaultPath) && File.ReadAllText(Prefs.DefaultPath).Contains("\"b\": 2"));
    }

    [Fact]
    public void TheDefaultFileIsInTheDataFolderUnlessOverridden()
    {
        string? old = Prefs.PathOverride;
        try
        {
            Prefs.PathOverride = null;
            Assert.Equal(Path.Combine(TerrainEditor.App.AppSettings.DataDir, "prefs.json"), Prefs.DefaultPath);
            Prefs.PathOverride = File1;
            Assert.Equal(File1, Prefs.DefaultPath);
            Prefs.Load().Set("x", 1);
        }
        finally
        {
            Prefs.PathOverride = old;
        }
    }
}
