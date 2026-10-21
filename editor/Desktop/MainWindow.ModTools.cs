using Avalonia.Input;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Mods' own tools (ModExtensions.Tools): their card, the view's pointer and preview, their keys, chosen and left.
public partial class MainWindow
{
    /// <summary>What the mod's tool in use asks the editor through, made once.</summary>
    private ModToolContext? _modContext;

    /// <summary>The mod's tool told it was chosen last, to be told when it is left.</summary>
    private ModExtensions.Tool? _modInUse;

    /// <summary>The card's controls go to the tool in use.</summary>
    private void WireModTools()
    {
        _modContext = new ModToolContext(View, ModToolPanel, s => { Msg(s); UpdateSaveBar(); });
        ModToolPanel.Changed += id =>
        {
            if (_modInUse is { } tool)
            {
                tool.Control(_modContext, id);
                UpdateSaveBar();
            }
        };
    }

    /// <summary>
    /// The tool in use changed: a mod's tool left is told so (its preview cleared), one chosen gets its card and the
    /// view's pointer, and is told so.
    /// </summary>
    private void ModToolChanged()
    {
        var chosen = Tools.Mode == ToolMode.Mod ? Tools.ModTool : null;
        if (_modInUse != null && !ReferenceEquals(_modInUse, chosen))
        {
            _modInUse.Left(_modContext!);
            if (View.Scene is { } s)
            {
                View.SetModPreview(s, []);
            }
        }
        ModToolPanel.Card.IsVisible = chosen != null;
        View.ModTool = chosen != null ? (chosen, _modContext!) : null;
        if (chosen != null && !ReferenceEquals(_modInUse, chosen))
        {
            ModToolPanel.Show(chosen);
            _modInUse = chosen;
            chosen.Chosen(_modContext!);
        }
        _modInUse = chosen;
    }

    /// <summary>A key for the mod's tool in use; true when it used it.</summary>
    private bool ModToolKey(Key key, KeyModifiers mods)
    {
        if (_modInUse is not { } tool)
        {
            return false;
        }
        // Enter and Return are one key: always "Enter".
        bool used = tool.Pressed(_modContext!, key == Key.Enter ? "Enter" : key.ToString(), GlView.Keys(mods));
        if (used)
        {
            UpdateSaveBar();
        }
        return used;
    }
}
