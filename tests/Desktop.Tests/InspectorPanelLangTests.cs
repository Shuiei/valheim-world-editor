using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The object inspector in another language (the made-up one: PseudoLang), on the test world's chest (a value added,
/// an item put in a picked slot, every item's details) and on an armour stand and an item stand put next to it: only
/// catalogue texts show, but for the data (names of objects, items and values, and the tips made from them).
/// Applying says so in the language with its English kept for Claude, and the question about items outside the slots
/// asks in the language.
/// </summary>
[Collection("DataDir")]
public sealed class InspectorPanelLangTests
{
    /// <summary>The test world's chest.</summary>
    private static readonly int Chest = StableHash.Of("piece_chest_wood");

    /// <summary>Clicks the button and runs the posted work it starts.</summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// What the card shows as it is in every language: the object's and items' names, the values' names and notes (the
    /// prefab or item a number names), and the words of the tips made from them ("text · … · key 123", "Wood × 1 (slot
    /// 1, 1)").
    /// </summary>
    private static string[] Data(InspectorPanel ins, WorldScene scene, int index) =>
    [
        PrefabCatalog.DisplayName(scene.Things[index].Prefab) ?? "",
        .. ins.FieldList.Select(f => f.Name ?? ""),
        .. ins.FieldList.Select(f => f.Note ?? ""),
        .. ins.Items?.Select(i => i.Name) ?? [],
        .. ins.Stand?.Select(r => r.Name) ?? [],
        "key", "data", "slot",
    ];

    [AvaloniaFact]
    public async Task InspectorPanelShowsOnlyCatalogueTexts()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            using var pseudo = new PseudoLang();
            var world = WorldSave.Load(dir);
            var at = world.Objects.First(o => o.Prefab == Chest).Position;
            var scene = WorldScene.Load(dir, (int)MathF.Floor((at.X + 32) / 64), (int)MathF.Floor((at.Z + 32) / 64), 1);
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            w.View.Show(scene, null);
            w.Edit(scene.Session!);
            var s = scene.Session!;
            var ins = w.Inspector;
            int chest = scene.Things.FindLastIndex(t => t.Prefab == Chest && !t.Gone);
            var near = scene.Things[chest].Position + new Vector3(2, 0, 2);
            int[] stands = [.. s.Commit("Stands", null, [], [
                (new NewObject(0, StableHash.Of("ArmorStand"), near, Vector3.Zero, 0), true),
                (new NewObject(0, StableHash.Of("itemstand"), near + new Vector3(2, 0, 0), Vector3.Zero, 0), true),
            ])];

            // The chest: a value added, an item in a picked slot, every item's details.
            Assert.True(ins.Open(chest));
            ins.AddKey.Text = "text";
            ins.AddValue.Text = "Hello";
            Click(ins.AddButton);
            Click(ins.ItemsBox.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == PseudoLang.Wrap("Add item")));
            Click(ins.ItemsBox.GetLogicalDescendants().OfType<Button>().First(b => Tips.KeyOf(b) == "inspect.slot"));
            ins.ItemsBox.GetLogicalDescendants().OfType<Expander>().First().IsExpanded = true;
            Assert.Empty(PseudoLang.Untranslated(ins.Card, Data(ins, scene, chest)));

            // Each stand: its rows, the slot kinds an armour stand's rows take, the pose or the way it hangs.
            foreach (int stand in stands)
            {
                Assert.True(ins.Open(stand));
                Assert.Empty(PseudoLang.Untranslated(ins.Card, Data(ins, scene, stand)));
            }

            // Applying the chest's added value: said in the language, its English kept for Claude.
            Assert.True(ins.Open(chest));
            ins.Added.Add(("strings", "text", "Hello"));
            await ins.Apply();
            Assert.StartsWith("⟦", w.MessageText.Text, StringComparison.Ordinal);
            Assert.StartsWith("Changed ", w.LastMessageEn, StringComparison.Ordinal);
            Assert.DoesNotContain("⟦", w.LastMessageEn, StringComparison.Ordinal);

            // An item outside the slots: the question, its title and its button in the language.
            var asked = new List<string>();
            ins.Confirm = (title, text, yes) =>
            {
                asked.AddRange([title, text, yes]);
                return Task.FromResult(false);
            };
            Click(ins.ItemsBox.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == PseudoLang.Wrap("Add item")));
            ins.Items![^1].X = 99;
            await ins.Apply();
            Assert.Equal(3, asked.Count);
            Assert.All(asked, a => Assert.True(PseudoLang.Translated(a, []), a));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
