using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map page's zones and marks: the counts and marks that stay with their world, the pending and found lines, and the
/// unmarking.
/// </summary>
[Collection("World files")]
public class MapPageZonesTests
{
    // Zone counts still being read when another world is shown are the first world's: dropped, not matched against the
    // second; a question answered once another world is shown marks nothing in either.
    [AvaloniaFact]
    public async Task ZoneCountsAndMarksStayWithTheirWorld()
    {
        using var r = new MapPageRun();
        await r.Open();
        var first = r.W.World!;
        string otherDir = Fixtures.CopyFixture();
        try
        {
            var other = WorldSession.Open(otherDir);
            r.M.NoBuildBox.IsChecked = false;
            r.M.OnlyGenBox.IsChecked = false;
            r.M.NoEditBox.IsChecked = false;
            // Shown before the counts come back (they come back on this thread, after it).
            var showing = r.M.ShowMatching();
            r.M.Show(other);
            await showing;
            Assert.Empty(r.M.Matches);
            // The second world's own counts are read when asked.
            await r.M.ShowMatching();
            Assert.NotEmpty(r.M.Matches);
            // Back to the first, which switches to the second while asking.
            r.M.Show(first);
            r.M.Confirm = _ =>
            {
                r.M.Show(other);
                return Task.FromResult(true);
            };
            await r.M.MarkMatching();
            Assert.Equal(0, first.Pending.Resets);
            Assert.Equal(0, other.Pending.Resets);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(otherDir)!, recursive: true);
        }
    }

    // Zone counts still being read when the same world is shown again (after a save or a discard) are from before: dropped
    // (with their "Reading" status), and read again when next asked; a question answered once it is shown again marks
    // nothing.
    [AvaloniaFact]
    public async Task ZoneCountsAndMarksFromBeforeTheWorldIsShownAgainAreDropped()
    {
        using var r = new MapPageRun();
        await r.Open();
        r.M.NoBuildBox.IsChecked = false;
        r.M.OnlyGenBox.IsChecked = false;
        r.M.NoEditBox.IsChecked = false;
        // Shown again before the counts come back (they come back on this thread, after it).
        var showing = r.M.ShowMatching();
        r.M.Show(r.W.World!);
        await showing;
        Assert.Empty(r.M.Matches);
        Assert.Equal("", r.M.ZoneInfo.Text);
        var again = r.M.ShowMatching();
        Assert.Equal("Reading every zone…", r.M.ZoneInfo.Text);
        await again;
        Assert.NotEmpty(r.M.Matches);
        r.M.Confirm = _ =>
        {
            r.M.Show(r.W.World!);
            return Task.FromResult(true);
        };
        await r.M.MarkMatching();
        Assert.Equal(0, r.W.World!.Pending.Resets);
    }

    [AvaloniaFact]
    public async Task UnmarkingBeforeTheZonesAreReadStillWorks()
    {
        using var r = new MapPageRun();
        await r.Open();
        r.W.World!.Edits.SetReset(new ZoneReset(3, 4, true, false), true);
        await r.M.UnmarkAll();
        Assert.Equal(0, r.W.World.Pending.Resets);
        Assert.Equal("Everything is saved.", r.M.Pending.Text);
    }

    // The pending line: all saved or applied, else what waits, one zone or several.
    [Theory]
    [InlineData(0, 0, 0, 0, false, "Everything is saved.")]
    [InlineData(0, 0, 0, 0, true, "Everything is applied.")]
    [InlineData(1, 0, 0, 0, false, "Not saved yet: 1 zone of ground.")]
    [InlineData(3, 2, 1, 1, true, "Not applied yet: 3 zones of ground, 2 deleted, 1 added, 1 zone reset(s).")]
    [InlineData(0, 0, 4, 0, false, "Not saved yet: 4 added.")]
    public void ThePendingLineSaysWhatWaits(int zones, int deleted, int added, int resets, bool live, string said) =>
        Assert.Equal(said, MapPage.PendingText((zones, deleted, added, resets), live));

    // The found line: the counts (twelve at most, then how many more), and how many found, in all for items, with
    // how many are pinned when the search kept only the first.
    [Theory]
    [InlineData("kinds", false, 2, "2 Beech · 1 Kind1\n3 found. Click one to go there.")]
    [InlineData("kinds", true, 2, "2 Beech · 1 Kind1\n3 found, the first 2 pinned. Click one to go there.")]
    [InlineData("items", false, 2, "2 Beech · 1 Kind1\n3 found in all. Click one to go there.")]
    [InlineData("items", true, 2, "2 Beech · 1 Kind1\n3 found in all, the first 2 pinned. Click one to go there.")]
    [InlineData("texts", false, 14, "2 Beech · 1 Kind1 · 1 Kind2 · 1 Kind3 · 1 Kind4 · 1 Kind5 · 1 Kind6 · 1 Kind7 · 1 Kind8 · 1 Kind9 · 1 Kind10 · 1 Kind11 · +2 more\n15 found. Click one to go there.")]
    public void TheFoundLineSaysWhatWasFound(string what, bool truncated, int kinds, string said)
    {
        var counts = new Dictionary<string, int> { ["Beech"] = 2 };
        for (int i = 1; i < kinds; i++)
        {
            counts[$"Kind{i}"] = 1;
        }
        var hits = new List<WorldSearch.Hit> { new(1, "Beech", 0, 0, 0, null), new(2, "Beech", 1, 0, 1, null) };
        Assert.Equal(said, MapPage.Found(new WorldSearch.Result(hits, counts, counts.Values.Sum(), truncated), what));
    }
}
