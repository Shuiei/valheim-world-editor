using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's messages in another language (the made-up one: PseudoLang), each branch once: what to select
/// first, a runestone, the whole building, same kind, invert, a move (turned, lifted, cancelled, dropped, made), zones
/// (new, added, empty) and a mod's linked group (selected whole, kept at its height, moved and deleted whole). Each is
/// shown only in catalogue texts (the mod's own name for its group is data), and Claude reads its English.
/// </summary>
[Collection("DataDir")]
public sealed class SelectToolLangTests
{
    private static readonly int Wall = StableHash.Of("wood_wall_half"), Beech = StableHash.Of("Beech1"), Stone = StableHash.Of("Runestone_Meadows");

    [AvaloniaFact]
    public void EverySelectToolMessageIsInTheLanguageAndClaudeReadsEnglish()
    {
        using var pseudo = new PseudoLang();
        var v = SelectToolMouseTests.Open(
            SelectToolMouseTests.T(1, Wall, 30, 30, 50),
            SelectToolMouseTests.T(2, Beech, 52, 30, 52, false),
            SelectToolMouseTests.T(3, Stone, 26, 30, 30, false));
        var ship = TestScenes.AddShip(v.S);
        v.W.View.SetBoxes(ship.Select(i => SelectToolMouseTests.Box(v.S.Scene, i)));
        var said = new List<Said>();
        v.Tool.Message += (t, _) => said.Add(t);
        void Check(string english)
        {
            var last = said[^1];
            Assert.Empty(PseudoLang.Untranslated(new TextBlock { Text = last.Shown }, "vehicle"));
            Assert.StartsWith(english, last.English, StringComparison.Ordinal);
        }

        // Nothing selected.
        v.W.View.Select([]);
        v.Tool.Delete();
        Check("Select objects to delete first.");
        v.Tool.SameKind();
        Check("Select an object of each kind you want first.");
        v.Tool.WholeBuilding();
        Check("Select a building piece first (or double-click one).");

        // A runestone stays where it is.
        v.W.View.Select([2]);
        v.Tool.Lift(1);
        Check("Runestones cannot be moved or turned, only deleted");

        // The wall: its building, its kind, the others.
        v.W.View.Select([0]);
        v.Tool.WholeBuilding();
        Check("Selected the whole building: ");
        v.Tool.SameKind();
        Check("Selected every shown object of the same kind: ");
        v.Tool.Invert();
        Check("Selected the ");

        // The tree moved: turned and lifted, cancelled; dropped, then made.
        v.W.View.Select([1]);
        v.Tool.Turn(10);
        v.Tool.Lift(0.5f);
        Check("Moving 1 object(s): 0.0 m, turned 10°, +0.50 m");
        Assert.True(v.Tool.Cancel());
        Check("Move cancelled.");
        v.Tool.Drop();
        Check("Dropped 1 object(s): ");
        v.Tool.Commit();
        Check("Moved 1 object(s). Ctrl+Z puts them back.");

        // Zones on the ground: the wall alone, the runestone added, then nothing.
        v.Tool.ZoneOnGround = true;
        v.Drag(KeyModifiers.None, (26, 46), (34, 46), (34, 54), (26, 54), (26, 47));
        Check("Selected 1 object(s).");
        v.Drag(KeyModifiers.Control, (23, 27), (29, 27), (29, 33), (23, 33));
        Check("Selected 1 more object(s) (2 in all).");
        v.Drag(KeyModifiers.Control, (20, 20), (24, 20), (24, 24), (20, 24));
        Check("Nothing shown there.");

        // The ship, a group a mod links: selected whole, kept at its height, moved whole, deleted whole.
        v.Click(40, 38, clicks: 2);
        Check("Selected the whole vehicle: 5 part(s).");
        v.Tool.Drop();
        Check("Nothing to drop: a vehicle keeps its height.");
        v.Tool.Cancel();
        v.Drag(KeyModifiers.None, (40, 38), (43, 38), (45, 38));
        Assert.Contains(said, s => s.English.StartsWith("Moving 5 object(s) (a whole vehicle: a vehicle's parts never move alone)", StringComparison.Ordinal));
        Check("Moved 5 object(s) (a whole vehicle: a vehicle's parts never move alone). Ctrl+Z puts them back.");
        v.Tool.Delete();
        Check("Deleted 5 object(s) (a whole vehicle: a vehicle's parts never go alone).");
    }
}
