using System.Collections.Generic;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// A tool of the mod's own on the 3D editor's tool rail (see <see cref="IExtensionRegistry.AddTool"/>): a button, a
/// card of controls next to the rail, the pointer and keys while it is chosen, previews drawn over the area, and edits
/// that go on the editor's history like its own. Called on the window's thread: be quick, and leave long work to a
/// worker (<see cref="ISceneProvider"/> redraws what the edits change).
/// </summary>
public interface IEditorTool
{
    /// <summary>The tool's button: its id, name, help and icon.</summary>
    ToolInfo Info { get; }

    /// <summary>
    /// The controls of the tool's card, top to bottom: read once, when the tool is registered (their values are
    /// remembered by the editor between runs, by the tool's and the control's ids).
    /// </summary>
    IReadOnlyList<ToolControl> Controls { get; }

    /// <summary>The tool was chosen (its card shows).</summary>
    /// <param name="ctx">The editor, for this tool.</param>
    void OnChosen(IToolContext ctx);

    /// <summary>Another tool was chosen, or the area closed: take away previews (they are cleared anyway).</summary>
    /// <param name="ctx">The editor, for this tool.</param>
    void OnLeft(IToolContext ctx);

    /// <summary>
    /// The pointer over the 3D view: moved, pressed, dragged or released. The camera keeps the right and middle buttons
    /// and the wheel; the tool gets the left button, and every move.
    /// </summary>
    /// <param name="ctx">The editor, for this tool.</param>
    /// <param name="input">What happened, and what the pointer is over.</param>
    void OnPointer(IToolContext ctx, PointerInput input);

    /// <summary>A key pressed over the 3D view while the tool is chosen. True when the tool used it (the editor then does nothing with it).</summary>
    /// <param name="ctx">The editor, for this tool.</param>
    /// <param name="key">The key, as Avalonia names it ("Enter", "Escape", "Delete", "A"...).</param>
    /// <param name="mods">The modifier keys held.</param>
    bool OnKey(IToolContext ctx, string key, KeyModifiers mods);

    /// <summary>A control of the card was changed, or a button of it pressed.</summary>
    /// <param name="ctx">The editor, for this tool.</param>
    /// <param name="id">The control's id.</param>
    void OnControl(IToolContext ctx, string id);
}

/// <summary>A tool's button on the rail.</summary>
public sealed class ToolInfo
{
    /// <summary>A tool's button.</summary>
    /// <param name="id">The tool's id: letters, digits, dots, dashes and underscores, unique within the mod.</param>
    /// <param name="name">Its name, under the button and as the card's title.</param>
    /// <param name="help">What it does, shown when pointing at the button and at the top of its card.</param>
    public ToolInfo(string id, string name, string help)
    {
        Id = id;
        Name = name;
        Help = help;
    }

    /// <summary>The tool's id.</summary>
    public string Id { get; }

    /// <summary>Its name.</summary>
    public string Name { get; }

    /// <summary>What it does.</summary>
    public string Help { get; }

    /// <summary>
    /// Its icon: SVG path data in a 24 by 24 box, drawn as a line like the editor's own (null: the first letter of
    /// its name).
    /// </summary>
    public string? Icon { get; set; }
}

/// <summary>Modifier keys.</summary>
[System.Flags]
public enum KeyModifiers
{
    /// <summary>None held.</summary>
    None = 0,

    /// <summary>Shift.</summary>
    Shift = 1,

    /// <summary>Ctrl (Cmd on a Mac).</summary>
    Ctrl = 2,

    /// <summary>Alt.</summary>
    Alt = 4,
}
