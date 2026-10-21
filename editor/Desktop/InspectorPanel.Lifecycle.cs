using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>The card, hidden until an object is opened.</summary>
    /// <param name="session">Gives the current edit session (null when no world is open).</param>
    public InspectorPanel(Func<EditSession?> session)
    {
        _session = session;
        var close = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(6, 0) }.Tip("card.close");
        close.Click += (_, _) => Close();
        AddSection.Tip("inspect.addSection");
        AddKey.Tip("inspect.addKey");
        AddValue.Tip("inspect.addValue");
        AddButton.Tip("inspect.add");
        ApplyButton.Tip("inspect.apply");
        RevertButton.Tip("inspect.revert");
        AddButton.Click += (_, _) =>
        {
            string key = AddKey.Text?.Trim() ?? "";
            if (key == "")
            {
                Message?.Invoke(Said.Of("inspect.msg.typeName"));
                return;
            }
            if (ReadOnlyKey(key))
            {
                Message?.Invoke(Said.Of("inspect.msg.readOnly", key));
                return;
            }
            // One value a key: a second would be set over the first (or over the one in the list) on Apply.
            string section = Sections[Math.Max(0, AddSection.SelectedIndex)];
            int hash = ZdoKeys.Parse(key);
            if (FieldList.Exists(f => f.Section == section && f.Key == hash) || Added.Exists(a => a.Section == section && ZdoKeys.Parse(a.Key) == hash))
            {
                Message?.Invoke(Said.Of("inspect.msg.already", key));
                return;
            }
            Added.Add((section, key, AddValue.Text ?? ""));
            AddKey.Text = AddValue.Text = "";
            Render();
        };
        RevertButton.Click += (_, _) => Reset();
        ApplyButton.Click += async (_, _) => await Apply();
        Card = Ui.AsCard(new Border
        {
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            IsVisible = false,
            Child = new ScrollViewer
            {
                MaxHeight = 820,
                Content = new StackPanel
                {
                    Width = 380,
                    Spacing = 6,
                    Children =
                    {
                        new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _title, Ui.Col(close, 1) } },
                        _where,
                        ItemsBox,
                        new TextBlock { Text = Lang.T("inspect.head.data"), FontSize = 10, Foreground = Ui.Muted, Margin = new Thickness(0, 6, 0, 0) },
                        Fields,
                        new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,*,Auto"), ColumnSpacing = 4, Children = { AddSection, Ui.Col(AddKey, 1), Ui.Col(AddValue, 2), Ui.Col(AddButton, 3) } },
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ApplyButton, RevertButton } },
                        new TextBlock
                        {
                            Text = Lang.T("inspect.hint.apply"),
                            FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
            },
        });
    }

    /// <summary>True while the card is shown.</summary>
    public bool IsOpen => Card.IsVisible;

    /// <summary>Hides the card and forgets the object.</summary>
    public void Close()
    {
        Card.IsVisible = false;
        Index = null;
        Closed?.Invoke();
    }

    /// <summary>Raised when the card is closed.</summary>
    public event Action? Closed;
}
