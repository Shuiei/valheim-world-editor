using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The players of a live world in the 3D editor: they are shown and Go to moves to them, names are shown over
/// the players the camera sees, and an offline world has none.
/// </summary>
[Collection("World files")]
public class LivePlayersTests
{
    /// <summary>
    /// Raises the button's click (no posted work run).
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public async Task ALiveWorldShowsItsPlayersAndGoesToThem()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        await r.W.PollPlayers();
        Assert.True(r.W.PlayersRow.IsVisible);
        Assert.Equal(["Ada", "Bjorn"], r.W.LivePlayers.Select(p => p.Name));
        Assert.Equal(new Vector3(10, 30, -20), r.W.LivePlayers[0].World);
        Assert.Equal(r.W.LivePlayers, r.W.View.Players);
        Assert.Equal("Ada", r.W.PlayerBox.SelectedItem);
        Assert.True(r.W.GoPlayerButton.IsEnabled);
        // Ada is in the area: the camera turns to her.
        await r.W.GoToPlayer("Ada");
        var s = r.W.View.Scene!;
        Assert.Equal(0, s.X0);
        var t = r.W.View.Camera.Target;
        Assert.Equal(new Vector3(10 - s.Cx, 30, -(-20 - s.Cz)), t);
        Assert.Equal("Looking at Ada.", r.W.MessageText.Text);
        // Bjorn is outside: the area around him opens (his zone -1, 0).
        r.W.PlayerBox.SelectedItem = "Bjorn";
        Click(r.W.GoPlayerButton);
        await TestWindows.Until(() => r.W.View.Scene!.X0 == -1);
        await TestWindows.Until(() => r.W.MessageText.Text == "Looking at Bjorn.");
        Assert.Equal(0, r.W.View.Scene!.Z0);
        // Unknown or no name: nothing.
        await r.W.GoToPlayer("Nobody");
        await r.W.GoToPlayer(null);
        Assert.Equal("Looking at Bjorn.", r.W.MessageText.Text);
    }

    [AvaloniaFact]
    public async Task PlayersComeAndGoAndABrokenAnswerKeepsTheLast()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        await r.W.PollPlayers();
        r.W.PlayerBox.SelectedItem = "Bjorn";
        r.Game!.Players = "[{\"name\":\"Bjorn\",\"x\":1,\"y\":2,\"z\":3},{\"name\":\"Cid\",\"x\":4,\"y\":5,\"z\":6},{\"x\":9}]";
        await r.W.PollPlayers();
        Assert.Equal(["Bjorn", "Cid"], r.W.LivePlayers.Select(p => p.Name));
        // The one chosen stays chosen.
        Assert.Equal("Bjorn", r.W.PlayerBox.SelectedItem);
        r.Game.Players = "not json";
        await r.W.PollPlayers();
        Assert.Equal(2, r.W.LivePlayers.Count);
        r.Game.Fail["/players"] = 500;
        await r.W.PollPlayers();
        Assert.Equal(2, r.W.LivePlayers.Count);
        r.Game.Fail.Remove("/players");
        r.Game.Players = "[]";
        await r.W.PollPlayers();
        Assert.Empty(r.W.LivePlayers);
        Assert.False(r.W.GoPlayerButton.IsEnabled);
        Assert.Null(r.W.PlayerBox.SelectedItem);
    }

    [AvaloniaFact]
    public async Task NamesAreShownOverThePlayersTheCameraSees()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        await r.W.PollPlayers();
        var s = r.W.View.Scene!;
        // Looking east at Ada; Bjorn (west) is behind the camera.
        var ada = new Vector3(10 - s.Cx, 30, 20 + s.Cz);
        r.W.View.SetCamera(ada + new Vector3(-10, 3, 0), ada, 1600 / 1000f);
        r.W.PlaceLabels();
        var label = Assert.IsType<TextBlock>(Assert.Single(r.W.PlayerLabels.Children));
        Assert.Equal("Ada", label.Text);
        // Back on the map: no labels.
        r.W.ShowMap();
        r.W.PlaceLabels();
        Assert.Empty(r.W.PlayerLabels.Children);
    }

    [AvaloniaFact]
    public async Task AnOfflineWorldHasNoPlayers()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        Assert.False(r.W.PlayersRow.IsVisible);
        Assert.Empty(r.W.LivePlayers);
        await r.W.PollPlayers();
        Assert.Empty(r.W.View.Players);
        r.W.PlaceLabels();
        Assert.Empty(r.W.PlayerLabels.Children);
    }

    [Fact]
    public void APlayersPostIsABodyAndABeam()
    {
        var s = TestScenes.FlatScene();
        float[] posts = GlView.PlayerPosts(s, [("Ada", new Vector3(10, 30, -20))]);
        // 12 sides of two rings, 4 sides of the body, the beam: 29 lines of 6 numbers.
        Assert.Equal(29 * 6, posts.Length);
        // The beam: from the feet 40 m up, at the player's place in view space.
        float[] beam = posts[^6..];
        Assert.Equal(new[] { 10 - s.Cx, 30, 20 + s.Cz, 10 - s.Cx, 70, 20 + s.Cz }, beam);
        Assert.Empty(GlView.PlayerPosts(s, []));
    }
}
