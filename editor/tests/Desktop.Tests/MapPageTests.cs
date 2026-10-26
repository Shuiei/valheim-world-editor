using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map page: the messages it gives (the pending line for each count and for a live world, and the found line for
/// each kind of search), the biomes, the cursor line, the search, the zone filter, Show ruins and Show portals.
/// </summary>
[Collection("World files")]
public class MapPageTests
{
    /// <summary>
    /// What the map raises when the mouse moves over it (headless windows put an overlay above the map, so the event is
    /// raised as the map does).
    /// </summary>
    private static void Hovered(MapPage m, float x, float z) =>
        (typeof(MapView).GetField("Hovered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(m.Map) as Action<float, float>)!(x, z);

    [AvaloniaFact]
    public async Task WithoutAWorldNothingHappens()
    {
        var m = new MapPage();
        m.UpdatePending();
        await m.ShowMatching();
        await m.MarkMatching();
        await m.UnmarkAll();
        await m.Search();
        m.Pick(0, 0);
        Assert.Equal("Zone 0, 0 · ", m.PickTitle.Text);
        Assert.Empty(m.Matches);
        Assert.Equal("", m.SearchInfo.Text ?? "");
    }

    // Show ruins: off at first; it switches the map's ruins on and off.
    [AvaloniaFact]
    public void ShowRuinsSwitchesTheMapsRuins()
    {
        var m = new MapPage();
        Assert.False(m.RuinsBox.IsChecked);
        Assert.False(m.Map.ShowRuins);
        m.RuinsBox.IsChecked = true;
        Assert.True(m.Map.ShowRuins);
        m.RuinsBox.IsChecked = false;
        Assert.False(m.Map.ShowRuins);
    }

    // Show portals: off at first; it switches the map's portals on and off, and its text counts them once read.
    [AvaloniaFact]
    public async Task ShowPortalsSwitchesTheMapsPortalsAndCountsThem()
    {
        var m = new MapPage();
        Assert.False(m.PortalsBox.IsChecked);
        Assert.False(m.Map.ShowPortals);
        Assert.Equal("Show portals and their links", m.PortalsBox.Content);
        m.PortalsBox.IsChecked = true;
        Assert.True(m.Map.ShowPortals);
        m.PortalsBox.IsChecked = false;
        Assert.False(m.Map.ShowPortals);
        Assert.Equal("Show portals and their links (none)", MapPage.PortalsLabel(new PortalMap.Result([], [])));
        var two = new PortalMap.Result([new(1, "portal_wood", 0, 0, 0, "a", null), new(2, "portal_wood", 1, 0, 0, "a", null), new(3, "portal_wood", 2, 0, 0, "b", null)], [(0, 1)]);
        Assert.Equal("Show portals and their links (3: 1 linked pair(s), 1 alone)", MapPage.PortalsLabel(two));
        Assert.Equal("Show portals and their links (not read: locked)", MapPage.PortalsLabel(null, "locked"));

        using var r = new MapPageRun();
        await r.Open();
        for (int i = 0; i < 100 && r.M.Map.Portals == null; i++)
        {
            await Task.Delay(50);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        Assert.NotNull(r.M.Map.Portals);
        Assert.Null(r.M.Map.PortalsError);
        Assert.Equal(MapPage.PortalsLabel(r.M.Map.Portals), r.M.PortalsBox.Content);

        // Another world: the last one's portals and their counts go at once, before its own are read.
        using var other = WorldSession.Open(r.Dir);
        r.M.Show(other);
        Assert.Null(r.M.Map.Portals);
        Assert.Equal("Show portals and their links", r.M.PortalsBox.Content);
        for (int i = 0; i < 100 && r.M.Map.Portals == null; i++)
        {
            await Task.Delay(50);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        Assert.NotNull(r.M.Map.Portals);
    }

    // Show portals: a portal's tag is written over the map from PortalTagZoom metres a pixel in, not further out, and
    // only while the portal is on the map.
    [AvaloniaFact]
    public async Task PortalTagsAreWrittenOnlyZoomedInAndOnTheMap()
    {
        using var r = new MapPageRun();
        await r.Open();
        var zdo = new ZdoData { Prefab = StableHash.Of("portal_wood"), Position = new Vector3(123, 30, 456) };
        zdo.Set("strings", StableHash.Of("tag"), "home");
        r.W.World!.Edits.AddObjects([new NewObject(-1, zdo.Prefab, zdo.Position, Vector3.Zero, 0f, null, false, zdo.Serialize())]);
        r.M.Map.Refresh();
        for (int i = 0; i < 100 && r.M.Map.Portals?.Portals.Any(p => p.Id == -1) != true; i++)
        {
            await Task.Delay(50);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        Assert.Contains(r.M.Map.Portals!.Portals, p => p.Id == -1);
        r.M.PortalsBox.IsChecked = true;
        r.M.Map.LookAt(123, 456, MapPage.PortalTagZoom);
        Assert.Contains(r.M.Labels.Children.OfType<TextBlock>(), t => t.Text == "home");
        r.M.Map.LookAt(123, 456, MapPage.PortalTagZoom * 2);
        Assert.DoesNotContain(r.M.Labels.Children.OfType<TextBlock>(), t => t.Text == "home");
        // Close in, but far from it: off the map, no label.
        r.M.Map.LookAt(123 + 50_000 * MapPage.PortalTagZoom, 456, MapPage.PortalTagZoom);
        Assert.DoesNotContain(r.M.Labels.Children.OfType<TextBlock>(), t => t.Text == "home");
    }

    [AvaloniaFact]
    public async Task EachBiomeHasItsName()
    {
        using var r = new MapPageRun();
        await r.Open();
        var terrain = r.W.World!.Terrain;
        foreach (var (biome, name) in new[] { (ValheimGen.Heightmap.Biome.BlackForest, "Black Forest"), (ValheimGen.Heightmap.Biome.AshLands, "Ashlands"), (ValheimGen.Heightmap.Biome.DeepNorth, "Deep North"), (ValheimGen.Heightmap.Biome.Meadows, "Meadows") })
        {
            // The first zone of that biome on a walk across the world.
            (int X, int Z)? found = null;
            for (int z = -160; z <= 160 && found == null; z += 2)
            {
                for (int x = -160; x <= 160 && found == null; x += 2)
                {
                    if (terrain.BiomeAt(x * 64f, z * 64f) == biome)
                    {
                        found = (x, z);
                    }
                }
            }
            Assert.NotNull(found);
            r.M.Pick(found.Value.X, found.Value.Z);
            Assert.EndsWith(name, r.M.PickTitle.Text);
        }
    }

    [AvaloniaFact]
    public async Task TheCursorLineFollowsTheMouseOverTheMap()
    {
        // No world yet: nothing to say.
        var empty = new MapPage();
        Hovered(empty, 10, 10);
        Assert.Equal("", empty.Cursor.Text ?? "");
        using var r = new MapPageRun();
        await r.Open();
        Hovered(r.M, 100.4f, -40.6f);
        Assert.Matches(@"^x 100, z -41 · zone 2, -1 · \w", r.M.Cursor.Text);
    }

    [AvaloniaFact]
    public async Task AnEmptySearchClearsThePinsAndAFailedOneSaysSo()
    {
        using var r = new MapPageRun();
        // A sign with a text, saved into the world first (the test world has none).
        var world = WorldSave.Load(r.Dir);
        var (id, prefab, position, rotation, scale) = world.Objects.First(o => o.Prefab == StableHash.Of("Beech1"));
        var sign = new ZdoData { Prefab = StableHash.Of("sign"), Position = position + new System.Numerics.Vector3(2, 0, 2) };
        sign.Set("strings", StableHash.Of("text"), "Welcome home");
        Assert.True(WorldWriter.Save(world, [], added: [new NewObject(-1, sign.Prefab, sign.Position, default, 0, Fresh: false, Raw: sign.Serialize())]).Saved);
        await r.Open();
        r.M.SearchBox.Text = "beech";
        await r.M.Search();
        Assert.NotEmpty(r.M.Map.Pins);
        r.M.SearchBox.Text = "   ";
        await r.M.Search();
        Assert.Empty(r.M.Map.Pins);
        Assert.Equal("", r.M.SearchInfo.Text);
        Assert.Null(r.M.Hits.ItemsSource);
        // Texts are read from the chunk files: gone, the search fails with the reason.
        foreach (string f in Directory.GetFiles(r.Dir, "*.chunk"))
        {
            File.Delete(f);
        }
        r.M.SearchWhat.SelectedIndex = 2;
        r.M.SearchBox.Text = "a";
        await r.M.Search();
        Assert.StartsWith("Search failed: ", r.M.SearchInfo.Text);
    }

    [AvaloniaFact]
    public async Task TheZoneFilterSaysWhenNothingMatchesOrIsMarked()
    {
        using var r = new MapPageRun();
        await r.Open();
        await r.M.UnmarkAll();
        Assert.Equal("No zone is marked for reset.", r.Told.Single());
        // Nothing that far out: nothing to mark.
        r.M.RMinBox.Text = "99999";
        await r.M.MarkMatching();
        Assert.Equal("No zone matches the filter.", r.Told[1]);
        // A question answered no marks nothing.
        r.M.RMinBox.Text = "";
        r.M.NoBuildBox.IsChecked = false;
        r.M.OnlyGenBox.IsChecked = false;
        r.M.NoEditBox.IsChecked = false;
        r.M.GroundBox.IsChecked = true;
        string? asked = null;
        r.M.Confirm = q => { asked = q; return Task.FromResult(false); };
        await r.M.MarkMatching();
        Assert.Contains("and their ground edits undone", asked);
        Assert.Equal(0, r.W.World!.Pending.Resets);
        // The filter follows its boxes once shown.
        Assert.NotEmpty(r.M.Matches);
        r.M.RMaxBox.Text = "0";
        Assert.All(r.M.Matches, z => Assert.Equal((0, 0), z));
        // A distance that is no number (or not a finite one) is said, not taken as no limit; spaces between thousands
        // are fine.
        foreach (string bad in new[] { "abc", "NaN", "Infinity" })
        {
            r.M.RMaxBox.Text = bad;
            Assert.Empty(r.M.Matches);
            Assert.StartsWith("The distances from the centre must be numbers of metres", r.M.ZoneInfo.Text);
        }
        r.M.RMaxBox.Text = "";
        int all = r.M.Matches.Count;
        Assert.True(all > 0);
        r.M.RMaxBox.Text = "20 000";
        Assert.Equal(all, r.M.Matches.Count);
    }
}
