using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>Shows these players: in the view, the Go to list and the names over the view.</summary>
    internal void SetPlayers(IReadOnlyList<(string Name, Vector3 World)> players)
    {
        LivePlayers = players;
        View.Players = players;
        var names = players.Select(p => p.Name).ToList();
        string? chosen = PlayerBox.SelectedItem as string;
        if (!(PlayerBox.ItemsSource is List<string> old && old.SequenceEqual(names)))
        {
            PlayerBox.ItemsSource = names;
            PlayerBox.SelectedItem = chosen != null && names.Contains(chosen) ? chosen : names.FirstOrDefault();
        }
        GoPlayerButton.IsEnabled = names.Count > 0;
        ToolTip.SetTip(PlayerBox, Tips.Of("view.player") + "\n" + string.Join("\n", players.Select(p => $"{p.Name}: {p.World.X:0}, {p.World.Z:0}")));
        PlaceLabels();
    }

    /// <summary>The players' labels' text and backing colours.</summary>
    private static readonly IImmutableSolidColorBrush LabelInk = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#bfeaff")), LabelShade = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromArgb(150, 0, 0, 0));

    /// <summary>
    /// The players' names over their posts (4 m above their feet), where the camera sees them. The labels are kept
    /// from tick to tick (ten a second while live): moved and renamed, made only for more players, dropped when fewer
    /// are seen.
    /// </summary>
    internal void PlaceLabels()
    {
        int shown = 0;
        if (View.Scene is { } s && LivePlayers.Count > 0 && _pages.Content == _editorPage)
        {
            var size = View.Bounds.Size;
            foreach (var (name, w) in LivePlayers)
            {
                var q = Vector4.Transform(new Vector4(w.X - s.Cx, w.Y + 4, -(w.Z - s.Cz), 1), View.ViewProj);
                if (q.W <= 0.01f)
                {
                    continue;
                }
                double x = (q.X / q.W + 1) / 2 * size.Width, y = (1 - q.Y / q.W) / 2 * size.Height;
                if (x < 0 || y < 0 || x > size.Width || y > size.Height)
                {
                    continue;
                }
                if (shown == PlayerLabels.Children.Count)
                {
                    PlayerLabels.Children.Add(new TextBlock { FontSize = 13, FontWeight = FontWeight.Bold, Foreground = LabelInk, Background = LabelShade, Padding = new Thickness(4, 1) });
                }
                var label = (TextBlock)PlayerLabels.Children[shown++];
                label.Text = name;
                Canvas.SetLeft(label, x - 4 * name.Length - 4);
                Canvas.SetTop(label, y - 10);
            }
        }
        if (PlayerLabels.Children.Count > shown)
        {
            PlayerLabels.Children.RemoveRange(shown, PlayerLabels.Children.Count - shown);
        }
    }
}
