using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area panel through its own controls on a copy of the test world (zone 0, 0): every ground action chosen in its
/// list and applied, what it says with nothing to work on, Replace chosen from its lists, the height average, the
/// backups list ("Another folder…", a folder that is not a world, another world), a restore with a soft edge, Cancel
/// reset, Regrow's limits, and heightmaps.
/// </summary>
[Collection("World files")]
public class AreaPanelTests
{
    /// <summary>
    /// A main window on its own copy of the test world, opened in the Area tool with a zone selected unless told not
    /// to; deleted when disposed.
    /// </summary>
    private sealed class Run : IDisposable
    {
        public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
        public string Dir { get; } = Fixtures.CopyFixture();
        public EditSession S { get; }
        public AreaPanel P => W.AreaPanel;
        public string Said => W.MessageText.Text ?? "";

        public Run(bool select = true, int size = 1)
        {
            var scene = WorldScene.Load(Dir, 0, 0, size);
            W.Show();
            W.View.Show(scene, null);
            W.Edit(scene.Session!);
            S = scene.Session!;
            W.Tools.ChooseMode(ToolMode.Area);
            if (select)
            {
                Select(2, 2, 62, 62);
            }
        }

        /// <summary>
        /// Selects the box between two grid points, as a drag does: the points replaced, then one change notice.
        /// </summary>
        public void Select(float x0, float z0, float x1, float z1)
        {
            var points = W.View.Area.Points;
            lock (points)
            {
                points.Clear();
                points.Add(new Vector2(x0, z0));
                points.Add(new Vector2(x1, z1));
            }
            W.View.Area.Notify();
        }

        /// <summary>
        /// The ground height at grid point (x, z).
        /// </summary>
        public float H(int x, int z) => S.Scene.Heights[z * S.Scene.W + x];

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
    }

    /// <summary>
    /// Clicks the button and runs the posted work it starts.
    /// </summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTheory]
    [InlineData(AreaPanel.Act.Raise, "Raise applied to")]
    [InlineData(AreaPanel.Act.Lower, "Lower applied to")]
    [InlineData(AreaPanel.Act.Smooth, "Smooth applied to")]
    [InlineData(AreaPanel.Act.Natural, "Naturalize applied to")]
    [InlineData(AreaPanel.Act.Flatten, "Flatten applied to")]
    public async Task EachGroundActionIsAppliedFromThePanel(AreaPanel.Act act, string said)
    {
        using var r = new Run();
        float before = r.H(32, 32);
        r.P.Choose(act);
        r.P.HeightBox.Value = (decimal)(before + 3);
        r.P.AmountBox.Value = 1.5m;
        await r.P.Apply();
        Assert.StartsWith(said, r.Said);
        Assert.True(r.S.CanUndo);
        Assert.StartsWith("Area: ", r.S.UndoLabel);
        if (act == AreaPanel.Act.Raise)
        {
            Assert.Equal(before + 1.5f, r.H(32, 32), 2);
        }
        if (act == AreaPanel.Act.Lower)
        {
            Assert.Equal(before - 1.5f, r.H(32, 32), 2);
        }
        if (act == AreaPanel.Act.Flatten)
        {
            Assert.Equal(before + 3, r.H(32, 32), 2);
        }
    }

    [AvaloniaFact]
    public void NoLimitShowsUnderFlattenRaiseAndLowerAndIsTheBrushSwitch()
    {
        using var r = new Run();
        foreach (var act in new[] { AreaPanel.Act.Flatten, AreaPanel.Act.Raise, AreaPanel.Act.Lower })
        {
            r.P.Choose(act);
            Assert.True(r.P.NoLimitBox.IsVisible);
        }
        r.P.Choose(AreaPanel.Act.Smooth);
        Assert.False(r.P.NoLimitBox.IsVisible);
        // Flatten far above the ground: the limit leaves most of it out of reach, until No limit is on.
        r.P.Choose(AreaPanel.Act.Flatten);
        r.P.HeightBox.Value = (decimal)(r.H(32, 32) + 100);
        Assert.Contains("out of reach", r.P.VolumeText.Text);
        r.P.NoLimitBox.IsChecked = true;
        Assert.True(r.W.Tools.NoLimitBox.IsChecked);
        Assert.True(r.S.Ground.NoLimit);
        Assert.StartsWith("No limit", r.Said);
        Assert.DoesNotContain("out of reach", r.P.VolumeText.Text);
        // The brush options' switch moves this one too.
        r.W.Tools.NoLimitBox.IsChecked = false;
        Assert.False(r.P.NoLimitBox.IsChecked);
        Assert.False(r.S.Ground.NoLimit);
        Assert.Contains("out of reach", r.P.VolumeText.Text);
    }

    [AvaloniaFact]
    public async Task PaintIsAppliedWithTheKindChosenInItsList()
    {
        using var r = new Run();
        r.P.Choose(AreaPanel.Act.Paint);
        int paved = Array.FindIndex(((IEnumerable<string>)r.P.PaintBox.ItemsSource!).ToArray(), l => l.Contains("aved"));
        r.P.PaintBox.SelectedIndex = paved;
        await r.P.Apply();
        Assert.StartsWith("Paint paved applied to", r.Said);
        int p = 32 * r.S.Ground.W + 32;
        Assert.Equal(Brush.PaintOf(BrushTool.PaintPaved), r.S.Ground.Paint[(p * 4)..(p * 4 + 4)]);
    }

    [AvaloniaFact]
    public async Task RestoreAndErodeAreGroundActionsToo()
    {
        using var r = new Run();
        r.P.Choose(AreaPanel.Act.Raise);
        await r.P.Apply();
        r.P.Choose(AreaPanel.Act.Restore);
        await r.P.Apply();
        Assert.StartsWith("Restore", r.Said);
        r.P.Choose(AreaPanel.Act.Erode);
        await r.P.Apply();
        Assert.Contains("applied to", r.Said);
    }

    [AvaloniaFact]
    public async Task WithoutASelectionOrSessionItSaysWhatToDo()
    {
        using var r = new Run(select: false);
        r.P.Choose(AreaPanel.Act.Raise);
        await r.P.Apply();
        Assert.Equal("Select an area first.", r.Said);
        Assert.False(r.S.CanUndo);
        // No area open at all: nothing happens.
        var lone = new MainWindow(load: false) { Width = 800, Height = 600 };
        lone.Show();
        await lone.AreaPanel.Apply();
        Assert.Equal("", lone.MessageText.Text ?? "");
    }

    [AvaloniaFact]
    public async Task RemoveWithNoneOfTheChosenKindsInsideSaysSo()
    {
        using var r = new Run();
        foreach (var b in r.P.KindButtons.Values)
        {
            b.IsChecked = false;
        }
        r.P.Choose(AreaPanel.Act.Remove);
        await r.P.Apply();
        Assert.Equal("No objects of the chosen kinds inside the selection.", r.Said);
    }

    [AvaloniaFact]
    public async Task ReplaceUsesTheTwoLists()
    {
        using var r = new Run();
        r.P.Choose(AreaPanel.Act.Replace);
        r.P.Refresh();
        r.P.FromBox.SelectedIndex = -1;
        r.P.ToBox.SelectedIndex = -1;
        await r.P.Apply();
        Assert.Equal("Choose what to replace, and with what.", r.Said);
        var from = ((IEnumerable<object>)r.P.FromBox.ItemsSource!).Cast<string>().ToList();
        Assert.NotEmpty(from);
        r.P.FromBox.SelectedIndex = 0;
        string name = from[0].Split(" (")[0];
        int count = int.Parse(from[0].Split(" (")[1].TrimEnd(')'), CultureInfo.InvariantCulture);
        var to = ((IEnumerable<object>)r.P.ToBox.ItemsSource!).Cast<string>().ToList();
        r.P.ToBox.SelectedIndex = to.FindIndex(t => t != name);
        await r.P.Apply();
        Assert.StartsWith($"Replaced {count} object(s) with", r.Said);
        Assert.StartsWith($"Replaced {count} with", r.S.UndoLabel);
    }

    [AvaloniaFact]
    public void ReplacingAShipsRootTakesItsPartsAwayAndAPartGoesAlone()
    {
        using var r = new Run();
        var ship = TestScenes.AddShip(r.S, 0, 0);
        int beech = StableHash.Of("Beech1");
        int beeches = r.S.Scene.Things.Count(t => !t.Gone && t.Prefab == beech);
        r.P.Replace(r.S, [ship[0]], beech);
        Assert.StartsWith("Replaced 1 object(s) with ", r.Said);
        Assert.EndsWith(". Their 4 linked part(s) went with them.", r.Said);
        Assert.All(ship, i => Assert.True(r.S.Scene.Things[i].Gone));
        Assert.Equal(beech, r.S.Scene.Things[^1].Prefab);
        Assert.Equal(beeches + 1, r.S.Scene.Things.Count(t => !t.Gone && t.Prefab == beech));
        // One undo step brings the whole ship back. The root and a part given together: the four parts are counted.
        r.S.Undo();
        Assert.All(ship, i => Assert.False(r.S.Scene.Things[i].Gone));
        r.P.Replace(r.S, [ship[0], ship[1]], beech);
        Assert.StartsWith("Replaced 2 object(s) with ", r.Said);
        Assert.EndsWith(". Their 4 linked part(s) went with them.", r.Said);
        Assert.All(ship, i => Assert.True(r.S.Scene.Things[i].Gone));
        // A part replaced alone leaves the rest of the ship.
        r.S.Undo();
        r.P.Replace(r.S, [ship[1]], beech);
        Assert.DoesNotContain("linked part", r.Said);
        Assert.Equal([ship[1]], ship.Where(i => r.S.Scene.Things[i].Gone));
    }

    [AvaloniaFact]
    public void TheWithListIsWorkedOutAgainForAnotherWorld()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        // Two worlds, each with a kind of its own to copy (named here: the game's catalogue does not place them); the
        // first is the live snapshot, for a live merge to bring it a kind.
        int first = StableHash.Of("mod_first"), second = StableHash.Of("mod_second"), live = StableHash.Of("mod_live");
        string? Name(int h) => h == first ? "mod_first" : h == second ? "mod_second" : h == live ? "mod_live" : Terrain.PrefabCatalog.DisplayName(h);
        var p = new AreaPanel(w.View, () => w.Session, Name);
        var a = TestScenes.Flat(1, new WorldScene.Thing(first, first, new Vector3(10, 30, 10), Vector3.Zero, 0, true));
        a.Scene.World = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        a.Scene.World.Templates[first] = 0;
        var b = TestScenes.Flat(1, new WorldScene.Thing(second, second, new Vector3(10, 30, 10), Vector3.Zero, 0, true));
        b.Scene.World.Templates[second] = 0;
        w.View.Show(a.Scene, null);
        w.Edit(a);
        p.UsePicked(first);
        var with = ((IEnumerable<object>)p.ToBox.ItemsSource!).Cast<string>().ToList();
        Assert.Contains("mod_first", with);
        Assert.DoesNotContain("mod_second", with);
        Assert.DoesNotContain("mod_live", with);
        Assert.Equal("mod_first", p.ToBox.SelectedItem);
        // The game brings the same world a kind it had none of: offered after the next refresh, the choice kept.
        Assert.NotNull(a.Scene.World.MergeLive([(10, 10)], [(4242, 1, MadeObjects.Piece("mod_live", new Vector3(640, 30, 640), 777, "Astrid"))], _ => false, _ => false));
        p.Refresh();
        with = [.. ((IEnumerable<object>)p.ToBox.ItemsSource!).Cast<string>()];
        Assert.Contains("mod_live", with);
        Assert.Equal("mod_first", p.ToBox.SelectedItem);
        // A kind both worlds have (one the game places) stays chosen in the other world; the first world's own kinds
        // are not offered there.
        int shared = Terrain.PrefabCatalog.Placeable.Select(i => StableHash.Of(i.Name)).First(h => Name(h) != null);
        p.UsePicked(shared);
        w.View.Show(b.Scene, null);
        w.Edit(b);
        p.Refresh();
        with = [.. ((IEnumerable<object>)p.ToBox.ItemsSource!).Cast<string>()];
        Assert.DoesNotContain("mod_first", with);
        Assert.DoesNotContain("mod_live", with);
        Assert.Contains("mod_second", with);
        Assert.Equal(Terrain.PrefabCatalog.DisplayName(shared), p.ToBox.SelectedItem);
        p.UsePicked(second);
        Assert.Equal("mod_second", p.ToBox.SelectedItem);
        w.View.Show(a.Scene, null);
        w.Edit(a);
        p.Refresh();
        Assert.Equal(-1, p.ToBox.SelectedIndex);
        // No world open: the list is empty and the last world is let go of.
        w.View.Show(TestScenes.FlatScene(), null);
        p.Refresh();
        Assert.Empty((IEnumerable<object>)p.ToBox.ItemsSource!);
    }

    [AvaloniaFact]
    public void TheAverageButtonPutsTheMeanHeightInTheBox()
    {
        using var r = new Run();
        Click(r.P.AverageButton);
        float avg = r.W.View.Area.Average(r.S.Ground)!.Value;
        Assert.Equal((decimal)MathF.Round(avg, 1), r.P.HeightBox.Value);
    }

    [AvaloniaFact]
    public async Task TheBackupListOffersAnotherFolder()
    {
        using var r = new Run();
        r.P.Choose(AreaPanel.Act.Backup);
        var items = ((IEnumerable<object>)r.P.BackupBox.ItemsSource!).Cast<string>().ToList();
        Assert.Equal("Another folder…", items.Last());
        // Nothing chosen yet.
        await r.P.Apply();
        Assert.Equal("Choose a backup first.", r.Said);
        // Another folder, cancelled: nothing chosen.
        r.P.PickFolder = () => Task.FromResult<string?>(null);
        r.P.BackupBox.SelectedIndex = items.Count - 1;
        await TestWindows.Until(() => r.P.BackupBox.SelectedIndex == -1);
        // A folder that is not a world: said in the panel and in the message.
        string notAWorld = Path.Combine(Path.GetDirectoryName(r.Dir)!, "empty");
        Directory.CreateDirectory(notAWorld);
        int asked = 0;
        r.P.PickFolder = () => { asked++; return Task.FromResult<string?>(notAWorld); };
        r.P.BackupBox.SelectedIndex = r.P.BackupBox.ItemCount - 1;
        await TestWindows.Until(() => r.P.BackupBox.SelectedIndex >= 0 && ((IEnumerable<object>)r.P.BackupBox.ItemsSource!).Cast<string>().Contains(notAWorld));
        // Asked once: the new list used to keep "Another folder…" selected and ask again, forever.
        Assert.Equal(1, asked);
        Assert.Equal(notAWorld, ((IEnumerable<object>)r.P.BackupBox.ItemsSource!).Cast<string>().ElementAt(r.P.BackupBox.SelectedIndex));
        await r.P.Apply();
        Assert.StartsWith("That folder is not a world save that can be read", r.P.BackupInfo.Text);
        Assert.Equal(r.P.BackupInfo.Text, r.Said);
    }

    [AvaloniaFact]
    public async Task ABackupOfAnotherWorldIsRefused()
    {
        using var r = new Run();
        var other = WorldSave.Load(r.Dir);
        typeof(WorldSave).GetProperty(nameof(WorldSave.Seed))!.SetValue(other, other.Seed + 1);
        typeof(WorldSave).GetProperty(nameof(WorldSave.SeedName))!.SetValue(other, "Other");
        Assert.Null(await r.P.OpenBackup(r.Dir, other));
        Assert.StartsWith("That is another world (seed ", r.P.BackupInfo.Text);
        // The same world is read once, then kept.
        Assert.NotNull(await r.P.OpenBackup(r.Dir, r.S.Scene.World));
        Assert.StartsWith("Backup: save #", r.P.BackupInfo.Text);
        r.P.BackupInfo.Text = "";
        Assert.NotNull(await r.P.OpenBackup(r.Dir, r.S.Scene.World));
        Assert.Equal("", r.P.BackupInfo.Text);
        // Kept, it is still refused for another world (the same folder chosen after opening that one).
        Assert.Null(await r.P.OpenBackup(r.Dir, other));
        Assert.StartsWith("That is another world (seed ", r.P.BackupInfo.Text);
    }

    [AvaloniaFact]
    public async Task ARestoreWithASoftEdgeBlendsTowardsTheBackup()
    {
        using var r = new Run();
        r.Select(10, 10, 54, 54);
        r.P.SoftSlider.Value = 10;
        float was = r.H(32, 32), wasEdge = r.H(12, 32), wasOut = r.H(6, 32);
        // Raised and painted everywhere, saved: the backup is the world before.
        r.S.Shape(32, 32, Formula.Compile("3", []), 40, 0, "x");
        r.S.EditGround("paint", g =>
        {
            var touched = new List<int>();
            for (int z = 5; z < 60; z++)
            {
                for (int x = 5; x < 60; x++)
                {
                    int p = z * g.W + x;
                    g.PMod[p] = 1;
                    Array.Copy(Brush.PaintOf(BrushTool.PaintDirt)!, 0, g.Paint, p * 4, 4);
                    touched.Add(p);
                }
            }
            return (touched, (4, 4, 61, 61));
        });
        // The game's backup from before, then saved.
        TempWorld.CopyDir(r.Dir, r.Dir.TrimEnd(Path.DirectorySeparatorChar) + "_backup_auto-20261001100000");
        Assert.True(r.S.Save().Saved);
        var backup = Assert.Single(Backups.Find(r.Dir));
        foreach (var b in r.P.KindButtons.Values)
        {
            b.IsChecked = false;
        }
        await r.P.RestoreBackup(r.S, r.W.View.Area.Polygon()!, backup.Path);
        Assert.StartsWith("Restored from the backup: the ground", r.Said);
        // The middle as it was; the soft edge part way; outside the selection, still raised.
        Assert.Equal(was, r.H(32, 32), 2);
        float edge = r.H(12, 32);
        Assert.InRange(edge, wasEdge + 0.05f, wasEdge + 2.95f);
        Assert.Equal(wasOut + 3, r.H(6, 32), 2);
        int mid = 32 * r.S.Ground.W + 32;
        Assert.Equal(0, r.S.Ground.PMod[mid]);
    }

    [AvaloniaFact]
    public async Task ARestoreOfNeitherGroundNorObjectsDoesNothing()
    {
        using var r = new Run();
        r.P.BackupGroundBox.IsChecked = false;
        r.P.BackupObjectsBox.IsChecked = false;
        await r.P.RestoreBackup(r.S, r.W.View.Area.Polygon()!, r.Dir);
        Assert.Equal("Nothing to restore: the selection is as it was in the backup.", r.Said);
        Assert.False(r.S.CanUndo);
    }

    [AvaloniaFact]
    public async Task CancelResetWithNothingMarkedSaysSoAndResetAsksFirst()
    {
        using var r = new Run();
        Click(r.P.UnresetButton);
        Assert.Equal("No zone marked for reset under the selection.", r.Said);
        r.P.Confirm = _ => Task.FromResult(false);
        r.P.Choose(AreaPanel.Act.Reset);
        await r.P.Apply();
        Assert.Empty(r.S.Resets);
        r.P.Confirm = _ => Task.FromResult(true);
        r.P.ResetGroundBox.IsChecked = false;
        await r.P.Apply();
        Assert.Single(r.S.Resets);
        Click(r.P.UnresetButton);
        Assert.Empty(r.S.Resets);
        Assert.StartsWith("Cancelled reset of 1 zone(s)", r.S.UndoLabel);
    }

    [AvaloniaFact]
    public async Task AnApplyWhileOneRunsDoesNothing()
    {
        using var r = new Run();
        var answer = new TaskCompletionSource<bool>();
        int asked = 0;
        r.P.Confirm = _ =>
        {
            asked++;
            return answer.Task;
        };
        r.P.Choose(AreaPanel.Act.Reset);
        r.P.ResetGroundBox.IsChecked = false;
        var first = r.P.Apply();
        Assert.False(r.P.ApplyButton.IsEnabled);
        // Enter again while the question is open: not asked twice, not marked twice.
        await r.P.Apply();
        Assert.Equal(1, asked);
        answer.SetResult(true);
        await first;
        Assert.Single(r.S.Resets);
        Assert.True(r.P.ApplyButton.IsEnabled);
    }

    [AvaloniaFact]
    public async Task AnAreaOpenedWhileApplyWaitsGetsNothing()
    {
        using var r = new Run();
        EditSession? other = null;
        r.P.Confirm = _ =>
        {
            // Another area opened while the question is open.
            var scene = WorldScene.Load(r.Dir, 0, 0, 1);
            r.W.View.Show(scene, null);
            r.W.Edit(scene.Session!);
            other = scene.Session;
            return Task.FromResult(true);
        };
        r.P.Choose(AreaPanel.Act.Reset);
        r.P.ResetGroundBox.IsChecked = false;
        await r.P.Apply();
        Assert.Empty(r.S.Resets);
        Assert.Empty(other!.Resets);
        Assert.Equal("The area changed while this was running: nothing was applied.", r.Said);
    }

    [AvaloniaFact]
    public async Task RegrowNeedsTheGeneratorAndASmallEnoughArea()
    {
        // An area with no world generator behind it.
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseMode(ToolMode.Area);
        w.View.Area.Points.Add(new Vector2(2, 2));
        w.View.Area.Points.Add(new Vector2(60, 60));
        w.AreaPanel.Choose(AreaPanel.Act.Regrow);
        await w.AreaPanel.Apply();
        Assert.Equal("Regrow needs the world's generator (not available here).", w.MessageText.Text);
        // More than 16 zones.
        using var r = new Run(select: false, size: 5);
        r.Select(1, 1, 319, 319);
        r.P.Choose(AreaPanel.Act.Regrow);
        await r.P.Apply();
        Assert.StartsWith("Choose a smaller area", r.Said);
    }

    [AvaloniaFact]
    public async Task HeightmapsAreExportedToTheFolderAndBadPicturesRefused()
    {
        using var r = new Run();
        string folder = Path.Combine(Path.GetDirectoryName(r.Dir)!, "maps");
        r.P.HeightmapFolder = folder;
        Click(r.P.ExportButton);
        Assert.StartsWith($"Heightmap written to {folder}", r.Said);
        string png = Assert.Single(Directory.GetFiles(folder, "*.png"));
        // A picture that is not one.
        string bad = Path.Combine(folder, "bad.png");
        File.WriteAllText(bad, "not a picture");
        r.P.PickPicture = () => Task.FromResult<string?>(bad);
        Click(r.P.ImportButton);
        await TestWindows.Until(() => r.Said.StartsWith("That picture cannot be used", StringComparison.Ordinal));
        // The exported one, imported back over the whole area (no selection), then cancelled.
        r.W.View.Area.Clear();
        r.P.PickPicture = () => Task.FromResult<string?>(png);
        Click(r.P.ImportButton);
        await TestWindows.Until(() => r.P.PictureInfo.Text?.Contains("the whole area") == true);
        Click(r.P.CancelPictureButton);
        Click(r.P.PutButton);
        Assert.False(r.S.CanUndo);
        // Again, with the heights pushed above the game's limit: it says how many points stopped short.
        Click(r.P.ImportButton);
        await TestWindows.Until(() => r.P.PictureInfo.Text?.Contains("the whole area") == true);
        r.P.LowestBox.Value += 20;
        r.P.HighestBox.Value += 20;
        Click(r.P.PutButton);
        Assert.StartsWith("Imported, but", r.Said);
        Assert.Contains("could not reach their height", r.Said);
    }

    [AvaloniaFact]
    public async Task APictureReadIsDroppedWhenTheSelectionChangesAndRefusedForAnotherArea()
    {
        using var r = new Run();
        string folder = Path.Combine(Path.GetDirectoryName(r.Dir)!, "maps");
        r.P.HeightmapFolder = folder;
        Click(r.P.ExportButton);
        string png = Assert.Single(Directory.GetFiles(folder, "*.png"));
        r.P.PickPicture = () => Task.FromResult<string?>(png);
        Click(r.P.ImportButton);
        await TestWindows.Until(() => r.P.PictureInfo.Text?.Contains("the selection") == true);
        // The same outline again (the box redrawn where it is, Enter on a polygon that already has its points): the
        // picture stays.
        r.Select(2, 2, 62, 62);
        Assert.DoesNotContain("dropped", r.Said);
        // The selection changes: the picture was fitted to the old one.
        r.Select(2, 2, 30, 30);
        Assert.Equal("The picture read was dropped: the selection changed. Import it again.", r.Said);
        Click(r.P.PutButton);
        Assert.False(r.S.CanUndo);
        // Read again, then another area opens without the selection changing: its grid indices are the old ground's.
        Click(r.P.ImportButton);
        await TestWindows.Until(() => r.P.PictureInfo.Text?.Contains("the selection") == true);
        var scene = WorldScene.Load(r.Dir, 0, 0, 1);
        r.W.View.Show(scene, null);
        r.W.Edit(scene.Session!);
        Click(r.P.PutButton);
        Assert.Equal("The picture was fitted to another area: import it again.", r.Said);
        Assert.False(scene.Session!.CanUndo);
        Assert.False(r.S.CanUndo);
        // Dropped: putting again does nothing.
        r.W.MessageText.Text = "";
        Click(r.P.PutButton);
        Assert.Equal("", r.Said);
    }

    [AvaloniaFact]
    public void CopyPasteAndBlueprintButtonsAskTheWindow()
    {
        using var r = new Run();
        var asked = new List<string>();
        r.P.CopyAsked += () => asked.Add("copy");
        r.P.CutAsked += () => asked.Add("cut");
        r.P.PasteAsked += () => asked.Add("paste");
        r.P.SaveBlueprintAsked += () => asked.Add("save");
        r.P.LibraryAsked += () => asked.Add("library");
        Click(r.P.CopyButton);
        Click(r.P.CutButton);
        Click(r.P.PasteButton);
        Click(r.P.SaveBlueprintButton);
        Click(r.P.LibraryButton);
        Assert.Equal(new[] { "copy", "cut", "paste", "save", "library" }, asked);
    }

    [AvaloniaFact]
    public void BoxPolygonAndClearButtons()
    {
        using var r = new Run();
        Click(r.P.PolyButton);
        Assert.False(r.W.View.Area.Box);
        Assert.Empty(r.W.View.Area.Points);
        Click(r.P.BoxButton);
        Assert.True(r.W.View.Area.Box);
        r.Select(2, 2, 10, 10);
        Click(r.P.ClearButton);
        Assert.Empty(r.W.View.Area.Points);
        Assert.Equal("Selection cleared.", r.Said);
    }

    [AvaloniaFact]
    public void DraggingABoxCountsWhatIsInsideOnceTheMovesAreHandled()
    {
        using var r = new Run(select: false);
        var a = r.W.View.Area;
        a.Box = true;
        a.Down(new Vector2(2, 2), 1);
        a.Moved(new Vector2(12, 12));
        a.Moved(new Vector2(22, 22));
        // Not counted at each move: once the moves waiting are handled.
        Assert.DoesNotContain("m² selected", r.P.Info.Text ?? "", StringComparison.Ordinal);
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("400 m² selected", r.P.Info.Text, StringComparison.Ordinal);
        // Released: counted at once.
        a.Moved(new Vector2(32, 32));
        a.Up();
        Assert.StartsWith("900 m² selected", r.P.Info.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheVolumeIsWorkedOutOnlyForTheActionsShowingIt()
    {
        using var r = new Run();
        r.P.Choose(AreaPanel.Act.Remove);
        Assert.Equal("", r.P.VolumeText.Text);
        foreach (var act in (AreaPanel.Act[])[AreaPanel.Act.Flatten, AreaPanel.Act.Raise, AreaPanel.Act.Lower])
        {
            r.P.Choose(act);
            Assert.Contains("Ground inside", r.P.VolumeText.Text, StringComparison.Ordinal);
            r.P.Choose(AreaPanel.Act.Smooth);
            Assert.Equal("", r.P.VolumeText.Text);
        }
    }
}
