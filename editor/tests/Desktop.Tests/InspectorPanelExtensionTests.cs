using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The inspector and a mod's extension (ModExtensions): a byte-array value an extension reads is shown in its words
/// under the value; one it does not read, and any with no extension running, only by its size. Runs alone (the
/// extensions are shared; Settings reads them again).
/// </summary>
[Collection("DataDir")]
public sealed class InspectorPanelExtensionTests : IDisposable
{
    /// <summary>Puts no extension back in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>An extension reading "ropes" as a count, and refusing an empty value.</summary>
    private sealed class Counting : IWorldEditorExtension, IDataDecoder
    {
        public void Register(IExtensionRegistry registry) => registry.AddDecoder("ropes", this);

        public string? Describe(IObjectView obj, byte[] value) => value.Length == 0 ? null : $"{value.Length} rope(s) on {obj.Prefab}";
    }

    [AvaloniaFact]
    public void BytesAnExtensionReadsAreShownInItsWords()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var w = new MainWindow(load: false);
            var scene = WorldScene.Load(dir, 0, 0, 1);
            w.Show();
            w.View.Show(scene, null);
            w.Edit(scene.Session!);
            var at = new Vector3(5, 30, 5);
            byte[] raw = MadeObjects.Zdo("wood_floor", at, z =>
            {
                z.ByteList.Add((StableHash.Of("ropes"), [1, 2, 3]));
                z.ByteList.Add((StableHash.Of("other"), [4]));
            });
            int index = scene.Session!.Commit("Anchor", null, [], [(new NewObject(0, StableHash.Of("wood_floor"), at, Vector3.Zero, 0, null, false, raw), true)]).Single();
            var i = w.Inspector;

            Assert.True(i.Open(index));
            Assert.All(i.FieldList, f => Assert.Null(i.Decoded(f)));

            ModExtensions.UseForTests(("Test", new Counting()));
            Assert.True(i.Open(index));
            Assert.Equal("3 rope(s) on wood_floor", i.Decoded(i.FieldList.Single(f => f.Key == StableHash.Of("ropes"))));
            Assert.Null(i.Decoded(i.FieldList.Single(f => f.Key == StableHash.Of("other"))));
            Assert.Contains(i.Fields.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "3 rope(s) on wood_floor");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
