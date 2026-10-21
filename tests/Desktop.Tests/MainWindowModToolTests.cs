using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using ValheimWorldEditor.Sdk;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A mod's own tool in the window (ModExtensions.Tools): on the rail once registered, its card shown while it is chosen,
/// told when it is chosen and left, its card's buttons reaching it, and its edits one step of the area's history. Runs
/// alone (the extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowModToolTests : IDisposable
{
    /// <summary>Puts no extension back in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>A tool adding a fleet register (a data holder) when its button is pressed, counting what it is told.</summary>
    private sealed class Registers : IWorldEditorExtension, IEditorTool
    {
        public int Chosen { get; private set; }

        public int Left { get; private set; }

        public ToolInfo Info { get; } = new("registers", "Registers", "Adds a fleet register.");

        public IReadOnlyList<ToolControl> Controls { get; } = [new ButtonControl("add", "Add one")];

        public void Register(IExtensionRegistry registry) => registry.AddTool(this);

        public void OnChosen(IToolContext ctx) => Chosen++;

        public void OnLeft(IToolContext ctx) => Left++;

        public void OnPointer(IToolContext ctx, PointerInput input)
        {
        }

        public bool OnKey(IToolContext ctx, string key, KeyModifiers mods) => false;

        public void OnControl(IToolContext ctx, string id) =>
            ctx.Commit("A register", new ToolEdit().Add(ExampleMod.Fleet, new Vector3(5, 30, 5), Vector3.Zero, c => c.SetInt("ships", 2)));
    }

    [AvaloniaFact]
    public void AModsToolIsChosenFromTheRailAndEditsTheArea()
    {
        var tool = new Registers();
        ModExtensions.UseForTests(("Fleet", tool));
        var (w, s) = TestWindows.EditingFlat();
        w.Tools.SetModTools(ModExtensions.Tools);

        w.Tools.ChooseMod(Assert.Single(w.Tools.ModTools));
        Assert.Equal(ToolMode.Mod, w.Tools.Mode);
        Assert.True(w.ModToolPanel.Card.IsVisible);
        Assert.Equal(1, tool.Chosen);

        var add = w.ModToolPanel.Card.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Add one"));
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var step = Assert.Single(s.UndoList);
        Assert.Equal("A register", step.Label);
        int id = Assert.Single(step.Hidden).Id;
        Assert.False(s.Edits.IsGone(id));
        Assert.True(s.Undo());
        Assert.True(s.Edits.IsGone(id));

        w.Tools.ChooseMode(ToolMode.View);
        Assert.False(w.ModToolPanel.Card.IsVisible);
        Assert.Equal((1, 1), (tool.Chosen, tool.Left));
        w.Close();
    }
}
