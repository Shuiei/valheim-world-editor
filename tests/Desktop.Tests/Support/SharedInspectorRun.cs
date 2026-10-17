using System.Numerics;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A main window on its own copy of the test world, opened on the zone with the chest; deleted when disposed. The
/// object inspector tests share it.
/// </summary>
internal sealed class SharedInspectorRun : IDisposable
{
    /// <summary>The kind of the test world's chest.</summary>
    public static readonly int Chest = StableHash.Of("piece_chest_wood");

    public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
    public string Dir { get; } = Fixtures.CopyFixture();
    public WorldScene Scene { get; }
    public EditSession S => Scene.Session!;
    public InspectorPanel I => W.Inspector;
    public string? Said;

    public SharedInspectorRun()
    {
        var world = WorldSave.Load(Dir);
        var at = world.Objects.First(o => o.Prefab == Chest).Position;
        Scene = WorldScene.Load(Dir, (int)MathF.Floor((at.X + 32) / 64), (int)MathF.Floor((at.Z + 32) / 64), 1);
        W.Show();
        W.View.Show(Scene, null);
        W.Edit(Scene.Session!);
        I.Message += t => Said = t.Shown;
    }

    public int ChestIndex => Scene.Things.FindLastIndex(t => t.Prefab == Chest && !t.Gone);

    /// <summary>
    /// A new object of this kind beside the chest; its index.
    /// </summary>
    public int Add(string prefab)
    {
        var p = Scene.Things[ChestIndex].Position + new Vector3(2, 0, 2);
        return S.Commit("Add", null, [], [(new NewObject(0, StableHash.Of(prefab), p, Vector3.Zero, 0), true)])[0];
    }

    /// <summary>
    /// A new copy of the chest beside it, as an added object; its index.
    /// </summary>
    public int AddCopyOfChest() => S.Commit("Add", null, [], [(new NewObject(0, Scene.Things[ChestIndex].Prefab, Vector3.Zero, Vector3.Zero, 0), true)])[0];

    /// <summary>
    /// Deletes every file of the copy of the test world, so the object's file is gone.
    /// </summary>
    public void DeleteWorldFiles()
    {
        foreach (string f in Directory.GetFiles(Dir))
        {
            File.Delete(f);
        }
    }

    /// <summary>
    /// The first button with this label under the control.
    /// </summary>
    public static Button Labelled(Control c, string label) => c.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == label);

    /// <summary>
    /// Every text block's text under the control, joined with " | ", for asserting on what it shows.
    /// </summary>
    public static string Texts(Control c) => string.Join(" | ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
}
