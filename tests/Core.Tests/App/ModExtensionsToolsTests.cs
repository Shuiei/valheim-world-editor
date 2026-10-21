using TerrainEditor.App;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The tools running extensions put on the rail (ModExtensions.Tools): those with a button of the right form, the first
/// of an id, their controls of the right form; each call guarded, a tool that throws turning its extension off. Runs
/// alone (the extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ModExtensionsToolsTests : IDisposable
{
    /// <summary>Forgets the extensions in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>A tool with the button and controls given, throwing on a key when asked to.</summary>
    private sealed class Tool(ToolInfo info, params ToolControl[] controls) : IEditorTool
    {
        public ToolInfo Info => info;

        public IReadOnlyList<ToolControl> Controls => controls;

        public List<string> Calls { get; } = [];

        public void OnChosen(IToolContext ctx) => Calls.Add("chosen");

        public void OnLeft(IToolContext ctx) => Calls.Add("left");

        public void OnPointer(IToolContext ctx, PointerInput input) => Calls.Add("pointer " + input.Action);

        public bool OnKey(IToolContext ctx, string key, KeyModifiers mods) => key == "Boom" ? throw new InvalidOperationException("boom") : key == "Enter";

        public void OnControl(IToolContext ctx, string id) => Calls.Add("control " + id);
    }

    /// <summary>An extension registering tools.</summary>
    private sealed class Tools(params IEditorTool[] tools) : IWorldEditorExtension
    {
        public void Register(IExtensionRegistry registry)
        {
            foreach (var t in tools)
            {
                registry.AddTool(t);
            }
        }
    }

    [Fact]
    public void OnlyToolsAndControlsOfTheRightFormAreOffered()
    {
        var good = new Tool(new ToolInfo("dig", "Dig", "Digs."),
            new SliderControl("size", "Size", 1, 5, 2),
            new SliderControl("bad", "Bad", 5, 1, 2),
            new ChoiceControl("none", "None", []),
            new ToggleControl("size", "Twice", true),
            new ButtonControl("go!", "Go"),
            new TextControl("info", "Ready."));
        ModExtensions.UseForTests(("Mod", new Tools(
            good,
            new Tool(new ToolInfo("dig", "Again", "Same id.")),
            new Tool(new ToolInfo("bad id", "Bad", "Spaces.")),
            new Tool(new ToolInfo("noname", " ", "No name.")))));

        var tool = Assert.Single(ModExtensions.Tools);
        Assert.Equal(("Mod/dig", "Dig", "Mod"), (tool.Name, tool.Info.Name, tool.Mod));
        Assert.Equal(["size", "info"], tool.Controls.Select(c => c.Id));
    }

    [Fact]
    public void ACallThatThrowsTurnsTheExtensionOff()
    {
        var t = new Tool(new ToolInfo("dig", "Dig", "Digs."));
        ModExtensions.UseForTests(("Mod", new Tools(t)));
        var tool = Assert.Single(ModExtensions.Tools);
        tool.Chosen(null!);
        tool.Pointed(null!, new PointerInput(PointerAction.Press, KeyModifiers.None, default, default));
        tool.Control(null!, "x");
        Assert.True(tool.Pressed(null!, "Enter", KeyModifiers.Ctrl));
        Assert.False(tool.Pressed(null!, "Boom", KeyModifiers.None));
        Assert.False(tool.Running);
        tool.Left(null!);
        Assert.Equal(["chosen", "pointer Press", "control x"], t.Calls);
        Assert.Empty(ModExtensions.Tools);
    }
}
