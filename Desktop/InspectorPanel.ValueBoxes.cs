using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// A field's value box: the text it shows, typed values passed on to the field, and the mod's note as its tip.
    /// </summary>
    /// <param name="f">The field.</param>
    /// <param name="isItems">Whether the field is the contents, shown as "see below" while they are edited above.</param>
    /// <param name="ro">Whether the field is read-only bytes.</param>
    /// <param name="manifestReadOnly">Whether a mod's manifest shows the value only.</param>
    /// <param name="help">The mod's note for the value, or null.</param>
    /// <returns>The box.</returns>
    private TextBox ValueBox(Field f, bool isItems, bool ro, bool manifestReadOnly, string? help)
    {
        var box = new TextBox { Text = isItems ? Lang.T("inspect.seeContents") : ro ? f.Note : f.Now, IsReadOnly = ro || manifestReadOnly, FontSize = 11, Opacity = f.Removed ? 0.5 : 1 }.Tip("inspect.value");
        if (help != null)
        {
            ToolTip.SetTip(box, help);
        }
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !ro)
            {
                f.Now = box.Text ?? "";
                Dirty();
            }
        };
        return box;
    }
}
