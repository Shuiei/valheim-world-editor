using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What the world writer says in another language (the made-up one: PseudoLang): nothing to save, a save format it does
/// not write, a newer save of the game's, a save that failed and a save done are each shown in the language in use, and
/// their English is kept for Claude and the log.
/// </summary>
[Collection("DataDir")]
public sealed class WorldWriterLangTests
{
    /// <summary>A ground edit of this zone raising its point 500 by a metre.</summary>
    private static ZoneEdit Raised(TerrainZone t)
    {
        var e = new ZoneEdit(t.ZoneX, t.ZoneZ);
        e.Modified[500] = true;
        e.Level[500] = 1;
        return e;
    }

    /// <summary>The message is shown in the language in use (the result's Message too), its English as given.</summary>
    private static void Shown(WorldWriter.Result r, string english)
    {
        Assert.Equal(r.Report.Shown, r.Message);
        Assert.StartsWith(PseudoLang.Open.ToString(), r.Message);
        Assert.True(PseudoLang.Translated(r.Message, []), r.Message);
        Assert.StartsWith(english, r.Report.English);
        Assert.DoesNotContain(PseudoLang.Open, r.Report.English);
    }

    [Fact]
    public void WhatTheWriterSaysIsTranslatedWithItsEnglishKept()
    {
        using var pseudo = new PseudoLang();
        using (var w = new TempWorld())
        {
            var world = w.Load();
            Shown(WorldWriter.Save(world, []), "Nothing to save.");

            var other = w.Load();
            other.Chunks[0].WorldVersion = 40;
            Shown(WorldWriter.Save(other, [Raised(other.TerrainZones[0])]), "This world uses save format 40;");

            var newer = WorldWriter.Newer(w.Dir, world.SaveNumber - 1);
            Assert.NotNull(newer);
            Assert.True(PseudoLang.Translated(newer.Shown, []), newer.Shown);
            Assert.StartsWith($"Valheim saved this world (save #{world.SaveNumber})", newer.English);

            var saved = WorldWriter.Save(world, [Raised(world.TerrainZones[0])]);
            Assert.True(saved.Saved, saved.Message);
            Shown(saved, "Saved 1 zone(s) to save #");
        }
        using (var w = new TempWorld())
        {
            var world = w.Load();
            // The world's metadata file gone after loading: the save fails halfway.
            File.Delete(Path.Combine(w.Dir, $"_main.{world.SaveNumber}.fwl2"));
            Shown(WorldWriter.Save(world, [Raised(world.TerrainZones[0])]), "Saving failed (");
        }
    }
}
