using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class SettingsDialog
{
    private sealed partial class Form
    {
        /// <summary>
        /// A list of folder rows, each with its box, a Browse button and a remove button, and an add button under them.
        /// </summary>
        /// <param name="initial">The folders the list starts with.</param>
        /// <param name="pickTitle">The title of the folder picker its Browse buttons open.</param>
        /// <param name="read">Reads the folders in the boxes as they are now.</param>
        /// <returns>The list, ready to add to the window.</returns>
        private StackPanel BuildFolders(List<string> initial, string pickTitle, out Func<IEnumerable<string>> read)
        {
            var rows = new StackPanel { Spacing = 4 };
            var boxes = new List<TextBox>();
            void Add(string text)
            {
                var box = new TextBox { Text = text }.Tip("settings.folder");
                var x = new Button { Content = "✕" }.Tip("settings.remove");
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 4 };
                var br = new Button { Content = Lang.T("common.browse") }.Tip("settings.browse");
                br.Click += async (_, _) => { if (await Pick(pickTitle) is string p) { box.Text = p; } };
                Grid.SetColumn(br, 1);
                Grid.SetColumn(x, 2);
                row.Children.Add(box);
                row.Children.Add(br);
                row.Children.Add(x);
                x.Click += (_, _) => { rows.Children.Remove(row); boxes.Remove(box); };
                boxes.Add(box);
                rows.Children.Add(row);
            }
            foreach (string f in initial)
            {
                Add(f);
            }
            var add = new Button { Content = Lang.T("settings.addFolder") }.Tip("settings.add");
            add.Click += (_, _) => Add("");
            read = () => boxes.Select(b => b.Text ?? "");
            return new StackPanel { Spacing = 4, Children = { rows, add } };
        }
    }
}
