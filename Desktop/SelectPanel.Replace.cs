using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Replace with: the eyedropper's kind chosen in the list.
public sealed partial class SelectPanel
{
    /// <summary>The eyedropper's kind into "Replace with", when the game can make it.</summary>
    internal void UsePicked(int prefab)
    {
        int i = ReplaceKinds.IndexOf(prefab);
        string name = ReplaceBox.ItemsSource is IList<string> names && i >= 0 && i < names.Count ? names[i] : NameOf?.Invoke(prefab) ?? prefab.ToString();
        if (i < 0)
        {
            Message?.Invoke(Said.Of("select.msg.cannotPlace", Said.Raw(name)));
            return;
        }
        ReplaceBox.SelectedIndex = i;
        Message?.Invoke(Said.Of("select.msg.replaceWith", Said.Raw(name)));
    }
}
