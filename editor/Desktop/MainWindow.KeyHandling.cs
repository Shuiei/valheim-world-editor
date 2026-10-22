using Avalonia.Controls;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// The 3D editor's keys (shortcuts for the tools, the panels, undo, save and the like, see <see cref="KeyRules"/>);
    /// none while typing in a box or away from the editor; F1 (the help) everywhere.
    /// </summary>
    private void OnKey(object? sender, KeyEventArgs e)
    {
        // Typing in a box: its keys are its own. The keys below are the 3D editor's: on the start page or
        // the map they would act on the area left open behind it (an undo pushed live, unseen); under the busy veil
        // (saving, reading the world again) they would wait on it or do it twice.
        if (HelpKey(e) || e.Source is TextBox || _pages.Content != _editorPage || IsBusy)
        {
            return;
        }
        var press = new KeyPress(e.Key, e.KeyModifiers);
        foreach (var rule in _keyRules ??= KeyRules())
        {
            if (rule.Matches(press) && rule.Act(press))
            {
                if (rule.Handles)
                {
                    e.Handled = true;
                }
                return;
            }
        }
    }
}
